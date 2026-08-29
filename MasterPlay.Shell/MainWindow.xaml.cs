using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Core;

namespace MasterPlay.Shell;

/// <summary>
/// Vo desktop cho giao dien QuestLog (quyet dinh B1).
///
/// Day la APP THAT, khong phai tab trinh duyet: cua so rieng, icon rieng,
/// khong thanh dia chi, khong tab, F11 toan man hinh, chan dieu huong ra ngoai.
/// </summary>
public partial class MainWindow : Window
{
    private const double PreferredWidth = 1500;
    private const double PreferredHeight = 900;
    private const double WorkAreaUsage = 0.94;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;
    private const int DwmWindowCornerRound = 2;
    private const int WmDisplayChange = 0x007E;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmExitSizeMove = 0x0232;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    // Dev: Vite dev server. Doi bang bien moi truong khi can.
    private static readonly string AppUrl =
        Environment.GetEnvironmentVariable("QUESTORY_URL") ?? "http://localhost:5173/";

    private WindowState _stateBeforeFullscreen = WindowState.Normal;
    private bool _isFullscreen;
    private bool _consoleFullscreenSession;
    private bool _fullscreenBeforeConsoleSession;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        DpiChanged += OnDpiChanged;
        StateChanged += OnWindowStateChanged;
        PreviewKeyDown += OnKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitWindowToCurrentMonitor(usePreferredSize: true);

        // Giu du lieu duyet rieng cho app, khong dung chung voi Edge cua may.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuestLog", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await Web.EnsureCoreWebView2Async(env);

        var core = Web.CoreWebView2;
        core.WebMessageReceived += OnWebMessageReceived;

        var desktopSessionToken = Environment
            .GetEnvironmentVariable("QUESTLOG_DESKTOP_SESSION_TOKEN")?
            .Trim();
        if (desktopSessionToken is { Length: >= 32 })
        {
            var serializedToken = JsonSerializer.Serialize(desktopSessionToken);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "Object.defineProperty(globalThis, '__QUESTLOG_DESKTOP__', {" +
                $"value: Object.freeze({{ guestToken: {serializedToken} }}), " +
                "configurable: false, enumerable: false, writable: false });");
        }

        // Trong nhu app: bo menu chuot phai, bo devtools, khong cho mo cua so moi.
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        // Link ra ngoai -> mo bang trinh duyet he thong, KHONG dieu huong trong app.
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            OpenExternal(args.Uri);
        };

        core.NavigationStarting += (_, args) =>
        {
            if (!IsLocal(args.Uri))
            {
                args.Cancel = true;
                OpenExternal(args.Uri);
            }
        };

        core.NavigationCompleted += (_, args) =>
        {
            if (!args.IsSuccess) return;
            LoadingText.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
        };

        await WaitForServerAsync(AppUrl);
        core.Navigate(AppUrl);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            if (!root.TryGetProperty("type", out var type) ||
                type.GetString() != "questlog.console-fullscreen" ||
                !root.TryGetProperty("enabled", out var enabled) ||
                enabled.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return;
            }

            SetConsoleFullscreen(enabled.GetBoolean());
        }
        catch (JsonException)
        {
            // Bo qua message khong dung schema. Shell chi nhan lenh noi bo da biet.
        }
    }

    private void SetConsoleFullscreen(bool enabled)
    {
        if (enabled == _consoleFullscreenSession)
        {
            PostFullscreenState();
            return;
        }

        if (enabled)
        {
            _consoleFullscreenSession = true;
            _fullscreenBeforeConsoleSession = _isFullscreen;
            if (!_isFullscreen) ToggleFullscreen();
        }
        else
        {
            _consoleFullscreenSession = false;
            if (_isFullscreen != _fullscreenBeforeConsoleSession) ToggleFullscreen();
        }

        PostFullscreenState();
    }

    private void PostFullscreenState()
    {
        if (Web.CoreWebView2 == null) return;
        var payload = JsonSerializer.Serialize(new
        {
            type = "questlog.fullscreen-state",
            fullscreen = _isFullscreen
        });
        Web.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // WPF ap dung DPI moi truoc; doi mot nhip de WebView va kich thuoc cua so
        // cung dung chung he toa do cua man hinh vua chuyen sang.
        Dispatcher.BeginInvoke(() => FitWindowToCurrentMonitor(usePreferredSize: false));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
        ApplyWindowCornerPreference();
    }

    private void ApplyWindowCornerPreference()
    {
        var shouldRound = WindowState == WindowState.Normal && !_isFullscreen;
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is not null)
        {
            chrome.CornerRadius = shouldRound
                ? new CornerRadius(12)
                : new CornerRadius(0);
        }

        // Windows 11: de DWM cat ca bon goc cua khung native. Khong dung
        // AllowsTransparency vi no lam WebView2 mat tang toc phan cung.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var preference = shouldRound
            ? DwmWindowCornerRound
            : DwmWindowCornerDoNotRound;
        _ = DwmSetWindowAttribute(
            handle,
            DwmWindowAttributeCornerPreference,
            ref preference,
            Marshal.SizeOf<int>());
    }

    private IntPtr WindowMessageHook(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMaximizedWorkArea(windowHandle, lParam);
            handled = true;
        }
        else if (message is WmDisplayChange or WmExitSizeMove)
        {
            Dispatcher.BeginInvoke(() => FitWindowToCurrentMonitor(usePreferredSize: false));
        }

        return IntPtr.Zero;
    }

    private void ApplyMaximizedWorkArea(IntPtr windowHandle, IntPtr minMaxInfoPointer)
    {
        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo)) return;

        var bounds = _isFullscreen ? monitorInfo.MonitorArea : monitorInfo.WorkArea;
        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(minMaxInfoPointer);
        minMaxInfo.MaxPosition.X = bounds.Left - monitorInfo.MonitorArea.Left;
        minMaxInfo.MaxPosition.Y = bounds.Top - monitorInfo.MonitorArea.Top;
        minMaxInfo.MaxSize.X = bounds.Right - bounds.Left;
        minMaxInfo.MaxSize.Y = bounds.Bottom - bounds.Top;
        var dpiScale = GetDpiForWindow(windowHandle) / 96d;
        minMaxInfo.MinTrackSize.X = Math.Min(
            (int)Math.Round(MinWidth * dpiScale),
            minMaxInfo.MaxSize.X);
        minMaxInfo.MinTrackSize.Y = Math.Min(
            (int)Math.Round(MinHeight * dpiScale),
            minMaxInfo.MaxSize.Y);
        minMaxInfo.MaxTrackSize = minMaxInfo.MaxSize;
        Marshal.StructureToPtr(minMaxInfo, minMaxInfoPointer, fDeleteOld: false);
    }

    private void FitWindowToCurrentMonitor(bool usePreferredSize)
    {
        if (WindowState != WindowState.Normal) return;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo)) return;

        var dpiScale = GetDpiForWindow(handle) / 96d;
        if (dpiScale <= 0) dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        var workWidthPixels = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
        var workHeightPixels = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        var maxWidthPixels = (int)Math.Floor(workWidthPixels * WorkAreaUsage);
        var maxHeightPixels = (int)Math.Floor(workHeightPixels * WorkAreaUsage);

        var currentBounds = new NativeRect();
        var hasCurrentBounds = GetWindowRect(handle, ref currentBounds);
        var requestedWidthPixels = usePreferredSize
            ? (int)Math.Round(PreferredWidth * dpiScale)
            : hasCurrentBounds
                ? currentBounds.Right - currentBounds.Left
                : (int)Math.Round(ActualWidth * dpiScale);
        var requestedHeightPixels = usePreferredSize
            ? (int)Math.Round(PreferredHeight * dpiScale)
            : hasCurrentBounds
                ? currentBounds.Bottom - currentBounds.Top
                : (int)Math.Round(ActualHeight * dpiScale);

        var targetWidthPixels = Math.Min(requestedWidthPixels, maxWidthPixels);
        var targetHeightPixels = Math.Min(requestedHeightPixels, maxHeightPixels);

        // MinWidth/MinHeight chi la gioi han resize thu cong; man hinh rat nho
        // van duoc uu tien nam tron trong work area.
        targetWidthPixels = Math.Max(
            Math.Min((int)Math.Round(MinWidth * dpiScale), maxWidthPixels),
            targetWidthPixels);
        targetHeightPixels = Math.Max(
            Math.Min((int)Math.Round(MinHeight * dpiScale), maxHeightPixels),
            targetHeightPixels);

        Width = targetWidthPixels / dpiScale;
        Height = targetHeightPixels / dpiScale;

        var centeredLeft = monitorInfo.WorkArea.Left + (workWidthPixels - targetWidthPixels) / 2;
        var centeredTop = monitorInfo.WorkArea.Top + (workHeightPixels - targetHeightPixels) / 2;
        var left = usePreferredSize || !hasCurrentBounds
            ? centeredLeft
            : Math.Clamp(
                currentBounds.Left,
                monitorInfo.WorkArea.Left,
                monitorInfo.WorkArea.Right - targetWidthPixels);
        var top = usePreferredSize || !hasCurrentBounds
            ? centeredTop
            : Math.Clamp(
                currentBounds.Top,
                monitorInfo.WorkArea.Top,
                monitorInfo.WorkArea.Bottom - targetHeightPixels);

        SetWindowPos(
            handle,
            IntPtr.Zero,
            left,
            top,
            targetWidthPixels,
            targetHeightPixels,
            SwpNoZOrder | SwpNoActivate);
    }

    /// <summary>
    /// Dev server co the chua kip len khi app khoi dong — cho toi 30s roi moi bao loi,
    /// thay vi hien trang loi "khong ket noi duoc" gay hoang mang.
    /// </summary>
    private async Task WaitForServerAsync(string url)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 60; i++)
        {
            try
            {
                using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (res.IsSuccessStatusCode) return;
            }
            catch
            {
                // chua len, thu lai
            }
            await Task.Delay(500);
        }
    }

    private static bool IsLocal(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) &&
        (u.IsLoopback || u.Scheme == "about" || u.Scheme == "data");

    private static void OpenExternal(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // link hong thi bo qua, khong lam sap app
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeGlyph.Text = isMaximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = isMaximized ? "Restore" : "Maximize";
        AutomationProperties.SetName(MaximizeButton, isMaximized ? "Restore" : "Maximize");
        ApplyWindowCornerPreference();

        if (WindowState == WindowState.Normal && !_isFullscreen)
        {
            Dispatcher.BeginInvoke(() => FitWindowToCurrentMonitor(usePreferredSize: false));
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // F11 toan man hinh — giu dung thoi quen cua ban WPF cu.
        if (e.Key != Key.F11) return;
        e.Handled = true;
        ToggleFullscreen();
        PostFullscreenState();
    }

    private void ToggleFullscreen()
    {
        var chrome = WindowChrome.GetWindowChrome(this);

        if (_isFullscreen)
        {
            _isFullscreen = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            TitleBar.Visibility = Visibility.Visible;
            TitleBarRow.Height = new GridLength(36);
            if (chrome is not null) chrome.CaptionHeight = 36;
            WindowState = _stateBeforeFullscreen;
            Dispatcher.BeginInvoke(() => FitWindowToCurrentMonitor(usePreferredSize: false));
        }
        else
        {
            _stateBeforeFullscreen = WindowState;
            _isFullscreen = true;
            TitleBar.Visibility = Visibility.Collapsed;
            TitleBarRow.Height = new GridLength(0);
            if (chrome is not null) chrome.CaptionHeight = 0;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, ref NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
