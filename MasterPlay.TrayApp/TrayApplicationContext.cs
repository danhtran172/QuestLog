using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Integrations.Images;
using MasterPlay.Infrastructure.TrayControl;
using MasterPlay.TrayApp.Services;
using MasterPlay.TrayApp.Startup;
using MasterPlay.TrayApp.UI;
using MasterPlay.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
        // Backup save game da tam bo khoi tray -> con hai dich vu nen.
        private const int ServiceCount = 2;

        private readonly NotifyIcon _trayIcon;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrayApplicationContext> _logger;
        private readonly PlayTimeService _playTimeService;
        private readonly LibraryService _libraryService;
        private readonly SynchronizationContext? _uiContext;
        private readonly object _languageGate = new();
        private readonly List<(ToolStripMenuItem Item, TrayGlyph Glyph, TrayMenuRole Role)> _glyphItems = [];

        private ToolStripMenuItem? _headerMenuItem;
        private ToolStripMenuItem? _statusMenuItem;
        private ToolStripMenuItem? _playTimeMenuItem;
        private ToolStripMenuItem? _libraryMenuItem;
        private ToolStripMenuItem? _scanNowMenuItem;
        private ToolStripMenuItem? _syncImagesMenuItem;
        private ToolStripMenuItem? _openMasterPlayMenuItem;
        private ToolStripMenuItem? _openLogMenuItem;
        private ToolStripMenuItem? _startupMenuItem;
        private ToolStripMenuItem? _exitMenuItem;
        private ContextMenuStrip? _contextMenu;
        private FileSystemWatcher? _configWatcher;
        private System.Threading.Timer? _languageReloadTimer;
        private TrayShutdownSignal? _shutdownSignal;
        private TrayControlServer? _controlServer;

        private Font? _menuFont;
        private Font? _headerFont;
        private Font? _statusFont;
        private Font? _serviceFont;
        private TrayTheme _theme = TrayTheme.Dark;

        private bool _isSyncingImages = false;
        private bool _isExiting;

        public TrayApplicationContext(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<TrayApplicationContext>>();
            _playTimeService = serviceProvider.GetRequiredService<PlayTimeService>();
            _libraryService = serviceProvider.GetRequiredService<LibraryService>();

            _trayIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "QuestLog Tray",
                Visible = true
            };

            BuildContextMenu();
            ApplyTheme(ReadThemeFromConfig());
            _uiContext = SynchronizationContext.Current;
            WatchConfigChanges();
            ListenForShutdownRequest();

            // Mot service khong khoi dong duoc thi cai con lai van phai chay, va
            // menu phai hien dung trang thai that thay vi mac dinh "dang bat".
            StartService(_playTimeMenuItem, "playtime", () => _playTimeService.Start());
            _libraryService.GameDiscovered += OnGameDiscovered;
            StartService(_libraryMenuItem, "library scanner", () => _libraryService.Start());
            RefreshMenuState();
            ListenForControlRequests();

            _logger.LogInformation("Tray application started.");
        }

        private Icon LoadTrayIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "TrayIcon.ico");
            try
            {
                if (File.Exists(iconPath))
                {
                    // Lay dung frame cho khay theo DPI (16/20/24...), tranh de Windows
                    // thu nho frame 32px mac dinh lam vo icon.
                    using var stream = File.OpenRead(iconPath);
                    return new Icon(stream, SystemInformation.SmallIconSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load the tray icon from {IconPath}", iconPath);
            }

            try
            {
                // ExtractAssociatedIcon tra ve null duoc, va bam "!" vao do la mot
                // NullReferenceException lam chet tray ngay tu constructor.
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract the icon from the executable");
                return SystemIcons.Application;
            }
        }

        private void StartService(ToolStripMenuItem? menuItem, string serviceName, Action start)
        {
            try
            {
                start();
                if (menuItem != null)
                    menuItem.Checked = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start the {ServiceName} service", serviceName);
                if (menuItem != null)
                    menuItem.Checked = false;
            }
        }

        private void OnGameDiscovered(object? sender, LibraryGameNotification notification)
        {
            void ShowNotification()
            {
                var localizationKey = notification.Kind == LibraryGameNotificationKind.Added
                    ? "NewGameAdded"
                    : "NewGameDetected";
                ShowBalloon(5000, "QuestLog", LocalizationService.Get(localizationKey, notification.GameName), ToolTipIcon.Info);
            }

            if (_uiContext == null)
                ShowNotification();
            else
                _uiContext.Post(_ => ShowNotification(), null);
        }

        // Balloon co the duoc goi tu su kien nen sau khi tray da dong: luc do
        // NotifyIcon da dispose va ShowBalloonTip nem tren UI thread.
        private void ShowBalloon(int timeout, string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (_isExiting)
                    return;

                _trayIcon.ShowBalloonTip(timeout, title, text, icon);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not show the tray notification");
            }
        }

        // ------------------------------------------------------------------ menu
        //
        // Bo cuc bam theo bang Tray trong app (MasterPlay.Web/src/components/
        // TrayPanel.tsx): tieu de "Dich vu nen" kem dong trang thai, moi dich vu
        // la mot card co cong tac, roi den hang hanh dong (quet ngay, mo log,
        // dong bo anh). Ba muc rieng cua tray (mo app, khoi dong cung Windows,
        // thoat) web khong co nen gom xuong cuoi.
        private void BuildContextMenu()
        {
            _menuFont = SystemFonts.MenuFont ?? SystemFonts.DefaultFont;
            _contextMenu = new ContextMenuStrip
            {
                Font = _menuFont,
                ShowImageMargin = true,
                Padding = new Padding(0, 6, 0, 6)
            };

            var dpi = _contextMenu.DeviceDpi;
            int Scaled(int value) => (int)Math.Round(value * dpi / 96d);

            _contextMenu.ImageScalingSize = new Size(Scaled(16), Scaled(16));
            // Ti le chu theo web: tieu de 15px dam, ten dich vu 13px semibold,
            // dong trang thai 11px mau muted.
            _headerFont = new Font(_menuFont.FontFamily, _menuFont.Size + 2f, FontStyle.Bold);
            _statusFont = new Font(_menuFont.FontFamily, Math.Max(6f, _menuFont.Size - 0.5f));
            _serviceFont = CreateSemiboldFont(_menuFont);

            _contextMenu.HandleCreated += (_, _) => TrayMenuWindow.UseRoundedCorners(_contextMenu!);
            _contextMenu.Opening += (_, _) => RefreshMenuState();

            // Bam mot cong tac thi menu phai o lai: cong tac la thu de gat nhieu cai
            // lien tiep, dong menu sau moi lan gat la bat nguoi dung mo lai tu dau.
            //
            // Phai quyet dinh NGAY TAI DAY chu khong the dat co trong handler cua
            // muc: WinForms dong dropdown TRUOC roi moi ban su kien Click, nen luc
            // handler chay thi menu da tat roi.
            //
            // Chi chan ly do ItemClicked. Esc va bam ra ngoai la ly do khac, van
            // phai dong duoc, neu khong menu thanh cai bay.
            _contextMenu.Closing += (_, e) =>
            {
                if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
                    return;

                if (IsToggle(ClickedItem()))
                    e.Cancel = true;
            };

            // Le phai: bu cho chu bi day sang phai (icon nam trong le card) va chua
            // cho cong tac 34px cua dong dich vu / dong khoi dong cung Windows.
            var itemPadding = new Padding(Scaled(2), Scaled(5), Scaled(24), Scaled(5));
            var togglePadding = new Padding(Scaled(2), Scaled(5), Scaled(68), Scaled(5));
            var servicePadding = new Padding(Scaled(2), Scaled(11), Scaled(68), Scaled(11));

            _headerMenuItem = CreateItem(LocalizationService.Get("TrayServices"), TrayGlyph.Power, null, TrayMenuRole.Header);
            _headerMenuItem.Font = _headerFont;
            _headerMenuItem.Enabled = false;
            _headerMenuItem.Padding = new Padding(Scaled(2), Scaled(8), Scaled(24), Scaled(1));

            _statusMenuItem = CreateItem(string.Empty, null, null, TrayMenuRole.Subtitle);
            _statusMenuItem.Font = _statusFont;
            _statusMenuItem.Enabled = false;
            _statusMenuItem.Padding = new Padding(Scaled(2), 0, Scaled(24), Scaled(6));

            _playTimeMenuItem = CreateItem(LocalizationService.Get("PlayTimeTracking"), TrayGlyph.Activity, TogglePlayTime, TrayMenuRole.Service);
            _libraryMenuItem = CreateItem(LocalizationService.Get("LibraryScanner"), TrayGlyph.FolderSearch, ToggleLibrary, TrayMenuRole.Service);
            foreach (var item in new[] { _playTimeMenuItem, _libraryMenuItem })
            {
                item.Font = _serviceFont;
                item.Padding = servicePadding;
            }

            _scanNowMenuItem = CreateItem(LocalizationService.Get("ScanNow"), TrayGlyph.FolderSearch, ScanNow_Click);
            _openLogMenuItem = CreateItem(LocalizationService.Get("OpenLogFolder"), TrayGlyph.ScrollText, OpenLogFolder);
            _syncImagesMenuItem = CreateItem(LocalizationService.Get("SyncImagesCloudinary"), TrayGlyph.RefreshCw, SyncImagesToCloudinary_Click);

            _openMasterPlayMenuItem = CreateItem(LocalizationService.Get("OpenMasterPlay"), TrayGlyph.AppWindow, OpenMasterPlay);

            _startupMenuItem = CreateItem(LocalizationService.Get("StartWithWindows"), TrayGlyph.Power, ToggleStartWithWindows, TrayMenuRole.Toggle);
            _startupMenuItem.Checked = StartupHelper.IsStartupEnabled();
            _startupMenuItem.Padding = togglePadding;

            _exitMenuItem = CreateItem(LocalizationService.Get("Exit"), TrayGlyph.LogOut, Exit, TrayMenuRole.Danger);

            foreach (var item in new[] { _scanNowMenuItem, _openLogMenuItem, _syncImagesMenuItem, _openMasterPlayMenuItem, _exitMenuItem })
                item.Padding = itemPadding;

            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                _headerMenuItem,
                _statusMenuItem,
                _playTimeMenuItem,
                _libraryMenuItem,
                new ToolStripSeparator(),
                _scanNowMenuItem,
                _openLogMenuItem,
                _syncImagesMenuItem,
                new ToolStripSeparator(),
                _openMasterPlayMenuItem,
                _startupMenuItem,
                _exitMenuItem
            });

            _trayIcon.ContextMenuStrip = _contextMenu;
        }

        // Web dung font-semibold cho ten dich vu. Segoe UI co ban Semibold rieng;
        // may khong co thi lui ve chu thuong chu khong dung Bold (qua dam).
        private static Font CreateSemiboldFont(Font baseFont)
        {
            // Ten font khong co thi GDI+ am tham thay bang Microsoft Sans Serif,
            // nen phai so lai ten sau khi tao.
            var semibold = new Font("Segoe UI Semibold", baseFont.Size, FontStyle.Regular, baseFont.Unit);
            if (string.Equals(semibold.Name, "Segoe UI Semibold", StringComparison.OrdinalIgnoreCase))
                return semibold;

            semibold.Dispose();
            return new Font(baseFont, FontStyle.Regular);
        }

        private static bool IsToggle(ToolStripItem? item) =>
            item?.Tag is TrayMenuRole role && role is TrayMenuRole.Toggle or TrayMenuRole.Service;

        /// <summary>
        /// Muc vua duoc bam: lay theo muc nam duoi con tro, va neu con tro khong o
        /// tren menu (bam bang ban phim) thi lay muc dang duoc chon.
        /// </summary>
        private ToolStripItem? ClickedItem()
        {
            if (_contextMenu == null || _contextMenu.IsDisposed)
                return null;

            try
            {
                var underCursor = _contextMenu.GetItemAt(_contextMenu.PointToClient(Cursor.Position));
                if (underCursor != null)
                    return underCursor;
            }
            catch
            {
                // Menu dang dong giua chung, coi nhu khong co muc nao.
            }

            return _contextMenu.Items.OfType<ToolStripItem>().FirstOrDefault(item => item.Selected);
        }

        private ToolStripMenuItem CreateItem(
            string text,
            TrayGlyph? glyph,
            EventHandler? onClick,
            TrayMenuRole role = TrayMenuRole.Default)
        {
            var item = new ToolStripMenuItem(text) { Tag = role };
            if (onClick != null)
                item.Click += onClick;

            if (glyph.HasValue)
                _glyphItems.Add((item, glyph.Value, role));

            return item;
        }

        private void ApplyTheme(TrayTheme theme)
        {
            _theme = theme;
            if (_contextMenu == null || _contextMenu.IsDisposed)
                return;

            _contextMenu.Renderer = new TrayMenuRenderer(theme);
            _contextMenu.BackColor = theme.Surface;
            _contextMenu.ForeColor = theme.Foreground;
            ApplyGlyphs();
        }

        // Icon duoc ve lai moi lan doi theme vi mau nam trong chinh anh.
        private void ApplyGlyphs()
        {
            if (_contextMenu == null)
                return;

            var size = Math.Max(12, _contextMenu.ImageScalingSize.Width);
            foreach (var (item, glyph, role) in _glyphItems)
            {
                // Nhu web: icon dich vu mau primary, icon tieu de va nut hanh dong
                // mau chu, chi muc Thoat mau danger.
                var color = role switch
                {
                    TrayMenuRole.Service or TrayMenuRole.Toggle => _theme.Primary,
                    TrayMenuRole.Danger => _theme.Danger,
                    _ => _theme.Foreground
                };

                var previous = item.Image;
                item.Image = TrayGlyphs.Render(glyph, size, color);
                previous?.Dispose();
            }
        }

        private static TrayTheme ReadThemeFromConfig()
        {
            try
            {
                return TrayTheme.Resolve(JsonService.LoadConfig().ThemeMode);
            }
            catch
            {
                return TrayTheme.Resolve(null);
            }
        }

        private void RefreshMenuState()
        {
            if (_startupMenuItem != null)
                _startupMenuItem.Checked = StartupHelper.IsStartupEnabled();

            if (_statusMenuItem == null)
                return;

            var running = new[] { _playTimeMenuItem, _libraryMenuItem }
                .Count(item => item?.Checked == true);

            _statusMenuItem.Text = running == ServiceCount
                ? LocalizationService.Get("TrayRunning")
                : running == 0
                    ? LocalizationService.Get("TrayStopped")
                    : LocalizationService.Get("TrayPartialRunning", running, ServiceCount);

            // Chu doi theo ngon ngu nen be rong cung phai tinh lai moi lan.
            if (_contextMenu != null && !_contextMenu.IsDisposed)
                _contextMenu.MinimumSize = new Size(TrayMenuRenderer.MinimumMenuWidth(_contextMenu), 0);
        }

        private async void ToggleService(ToolStripMenuItem? menuItem, string serviceName, Action start, Action stop)
        {
            if (menuItem == null || !menuItem.Enabled)
                return;

            try
            {
                await SetServiceAsync(menuItem, serviceName, !menuItem.Checked, start, stop);
            }
            catch (Exception ex)
            {
                // Bat o day de mot loi khi bat/tat khong thoat ra khoi handler cua
                // menu va lam sap tray. Dung balloon chu khong phai MessageBox:
                // hop thoai cuop focus se dong menu dang mo.
                ShowBalloon(4000, LocalizationService.Get("Error"), ex.Message, ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// Dung chung cho menu va cho lenh tu QuestLog (TrayControlServer). Chay tren UI
        /// thread; menu item la noi giu trang thai bat/tat cua dich vu. Loi thi nem ra.
        /// </summary>
        private async Task SetServiceAsync(ToolStripMenuItem menuItem, string serviceName, bool enable, Action start, Action stop)
        {
            if (menuItem.Checked == enable)
                return;
            if (!menuItem.Enabled)
                throw new TrayBusyException();

            menuItem.Enabled = false;
            try
            {
                // Task.Run: Stop() cho worker ket thuc. Cho tren UI thread thi menu
                // dang mo dong bang, va day la luc nguoi dung dang nhin thang vao no.
                await Task.Run(enable ? start : stop);
                menuItem.Checked = enable;
                _logger.LogInformation(
                    enable ? "{ServiceName} enabled." : "{ServiceName} disabled.",
                    serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not toggle the {ServiceName} service", serviceName);

                // Stop() la idempotent nen de o trang thai tat: nguoi dung bam lai mot
                // lan nua la bat duoc, con de "dang bat" thi khong con duong quay lai.
                menuItem.Checked = false;
                throw;
            }
            finally
            {
                menuItem.Enabled = true;
                RefreshMenuState();
            }
        }

        private sealed class TrayBusyException : InvalidOperationException
        {
            public TrayBusyException() : base("The service is already being switched.") { }
        }

        private void TogglePlayTime(object? sender, EventArgs e) =>
            ToggleService(_playTimeMenuItem, "PlayTime tracking", _playTimeService.Start, _playTimeService.Stop);

        private void ToggleLibrary(object? sender, EventArgs e) =>
            ToggleService(_libraryMenuItem, "Library scanner", _libraryService.Start, _libraryService.Stop);

        private void ScanNow_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_libraryService.RequestScanNow())
                {
                    _logger.LogInformation("Manual library scan requested from the tray menu.");
                    ShowBalloon(3000, "QuestLog", LocalizationService.Get("ScanStarted"), ToolTipIcon.Info);
                }
                else
                {
                    ShowServiceStoppedBalloon("LibraryScanner");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start a manual library scan.");
                ShowBalloon(4000, LocalizationService.Get("Error"), ex.Message, ToolTipIcon.Error);
            }
        }

        private void ShowServiceStoppedBalloon(string serviceKey)
        {
            ShowBalloon(
                3000,
                "QuestLog",
                LocalizationService.Get(serviceKey) + " · " + LocalizationService.Get("TrayStopped"),
                ToolTipIcon.Warning);
        }

        private async void SyncImagesToCloudinary_Click(object? sender, EventArgs e)
        {
            if (_isSyncingImages)
            {
                ShowBalloon(3000, "QuestLog", LocalizationService.Get("ImageSyncAlreadyRunning"), ToolTipIcon.Info);
                return;
            }

            _isSyncingImages = true;
            _syncImagesMenuItem!.Enabled = false;
            _syncImagesMenuItem.Text = LocalizationService.Get("SyncingImages");

            _logger.LogInformation("Cloudinary image sync started from tray menu.");
            ShowBalloon(3000, "QuestLog", LocalizationService.Get("StartingImageSync"), ToolTipIcon.Info);

            try
            {
                var progress = new Progress<string>(msg => _logger.LogInformation("[ImageSync] {msg}", msg));
                // Admin -> Cloudinary, moi chu khac -> luu tren may (DOMAIN_RULES §12).
                var result = await GameImageSync.SyncAllAsync(progress);

                string summary = LocalizationService.Get(
                    "ImageSyncSummary",
                    result.CardCoverUploaded,
                    result.CardCoverAlreadySynced,
                    result.CardCoverMissing,
                    result.CoverUploaded,
                    result.CoverAlreadySynced,
                    result.CoverMissing,
                    result.PortraitCoverUploaded,
                    result.PortraitCoverAlreadySynced,
                    result.PortraitCoverMissing,
                    result.Failed,
                    result.TotalGames);

                foreach (var failure in result.Failures)
                    _logger.LogWarning("[ImageSync] {game} ({role}) failed: {reason}", failure.GameName, failure.Role, failure.Reason);
                _logger.LogInformation("Cloudinary image sync finished. {summary}", summary.Replace("\n", " "));
                ShowBalloon(6000, LocalizationService.Get("ImageSyncComplete"), summary, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Cloudinary image sync.");
                ShowBalloon(5000, LocalizationService.Get("ImageSyncError"), ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                _isSyncingImages = false;
                _syncImagesMenuItem.Enabled = true;
                _syncImagesMenuItem.Text = LocalizationService.Get("SyncImagesCloudinary");
            }
        }

        private void OpenLogFolder(object? sender, EventArgs e)
        {
            try
            {
                OpenLogFolderCore();
            }
            catch (Exception ex)
            {
                // Explorer bi chan hoac thu muc khong tao duoc: bao loi, khong sap tray.
                _logger.LogError(ex, "Could not open the log folder.");
                MessageBox.Show(
                    ex.Message,
                    LocalizationService.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void OpenLogFolderCore()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logPath);
            Process.Start("explorer.exe", logPath);
        }

        private void OpenMasterPlay(object? sender, EventArgs e)
        {
            try
            {
                using var runningProcess = Process.GetProcessesByName("QuestLog")
                    .FirstOrDefault(process => process.Id != Environment.ProcessId);
                if (runningProcess != null)
                {
                    var handle = runningProcess.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindow(handle, 9);
                        SetForegroundWindow(handle);
                    }
                    return;
                }

                var executablePath = FindMasterPlayExecutable();
                if (executablePath == null)
                    throw new FileNotFoundException(LocalizationService.Get("MasterPlayExecutableNotFound"));

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not open QuestLog UI.");
                MessageBox.Show(
                    LocalizationService.Get("CannotOpenMasterPlay", ex.Message),
                    LocalizationService.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string? FindMasterPlayExecutable()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>
            {
                Path.Combine(baseDirectory, "QuestLog.exe"),
                Path.Combine(baseDirectory, "Shell", "QuestLog.exe"),
                // Ban cai: QuestLog.exe o goc, tray nam trong thu muc con tray\.
                Path.Combine(baseDirectory, "..", "QuestLog.exe")
            };

            var outputDirectory = new DirectoryInfo(baseDirectory);
            var configurationDirectory = outputDirectory.Parent;
            var repositoryDirectory = outputDirectory.Parent?.Parent?.Parent?.Parent;
            if (configurationDirectory != null && repositoryDirectory != null)
            {
                candidates.Add(Path.Combine(
                    repositoryDirectory.FullName,
                    "MasterPlay.Shell",
                    "bin",
                    configurationDirectory.Name,
                    "net8.0-windows",
                    "QuestLog.exe"));
            }

            return candidates.FirstOrDefault(File.Exists);
        }

        private void ToggleStartWithWindows(object? sender, EventArgs e)
        {
            var enable = !StartupHelper.IsStartupEnabled();
            var succeeded = enable
                ? StartupHelper.EnableStartup(Application.ExecutablePath)
                : StartupHelper.DisableStartup();

            _startupMenuItem!.Checked = StartupHelper.IsStartupEnabled();

            // Bao bang balloon chu khong phai MessageBox: hop thoai cuop focus se
            // dong ngay cai menu vua co cong bat cho o lai.
            if (!succeeded)
            {
                ShowBalloon(
                    4000,
                    LocalizationService.Get("Error"),
                    LocalizationService.Get("StartupSettingFailed"),
                    ToolTipIcon.Error);
                return;
            }

            ShowBalloon(
                4000,
                "QuestLog",
                LocalizationService.Get(enable ? "WillStartWithWindows" : "WillNotStartWithWindows"),
                ToolTipIcon.Info);
        }

        private void WatchConfigChanges()
        {
            try
            {
                var configPath = JsonService.GetConfigPath();
                var directory = Path.GetDirectoryName(configPath);
                var fileName = Path.GetFileName(configPath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                    return;

                _configWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _configWatcher.Changed += (_, _) => ScheduleConfigReload();
                _configWatcher.Error += (_, e) =>
                    _logger.LogWarning(e.GetException(), "The config watcher reported an error");
            }
            catch (Exception ex)
            {
                // Theo doi config la tien ich, khong duoc lam chet tray.
                _logger.LogWarning(ex, "Could not watch config.json for language or theme changes");
            }
        }

        private void ScheduleConfigReload()
        {
            // Watcher goi tu nhieu thread: khong khoa thi hai thread cung dispose mot
            // Timer roi cham vao doi tuong da chet.
            lock (_languageGate)
            {
                if (_isExiting)
                    return;

                try
                {
                    _languageReloadTimer?.Dispose();
                    _languageReloadTimer = new System.Threading.Timer(_ => ReloadConfig(), null, 500, Timeout.Infinite);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not schedule the tray config reload");
                }
            }
        }

        // Ngon ngu va theme deu nam trong config.json va dung chung voi UI
        // (DOMAIN_RULES muc 15), nen doi ben UI la tray doi theo.
        private void ReloadConfig()
        {
            string language;
            string themeMode;
            try
            {
                var config = JsonService.LoadConfig();
                language = config.Language;
                themeMode = config.ThemeMode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read language or theme from config.");
                return;
            }

            void Apply()
            {
                try
                {
                    LocalizationService.SetLanguage(language);
                    ApplyLocalizedMenuText();

                    var theme = TrayTheme.Resolve(themeMode);
                    if (!string.Equals(theme.Name, _theme.Name, StringComparison.Ordinal))
                        ApplyTheme(theme);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not apply the tray language or theme.");
                }
            }

            // Dung SynchronizationContext chu khong phai ContextMenuStrip.BeginInvoke:
            // menu chua tung mo thi chua co handle va BeginInvoke nem ngay. Do la dong
            // canh bao "Could not reload tray language" trong log cu.
            if (_uiContext == null)
                Apply();
            else
                _uiContext.Post(_ => Apply(), null);
        }

        private void ApplyLocalizedMenuText()
        {
            if (_contextMenu == null || _contextMenu.IsDisposed)
                return;

            if (_headerMenuItem != null) _headerMenuItem.Text = LocalizationService.Get("TrayServices");
            if (_openMasterPlayMenuItem != null) _openMasterPlayMenuItem.Text = LocalizationService.Get("OpenMasterPlay");
            if (_playTimeMenuItem != null) _playTimeMenuItem.Text = LocalizationService.Get("PlayTimeTracking");
            if (_libraryMenuItem != null) _libraryMenuItem.Text = LocalizationService.Get("LibraryScanner");
            if (_scanNowMenuItem != null) _scanNowMenuItem.Text = LocalizationService.Get("ScanNow");
            if (_syncImagesMenuItem != null && !_isSyncingImages) _syncImagesMenuItem.Text = LocalizationService.Get("SyncImagesCloudinary");
            if (_openLogMenuItem != null) _openLogMenuItem.Text = LocalizationService.Get("OpenLogFolder");
            if (_startupMenuItem != null) _startupMenuItem.Text = LocalizationService.Get("StartWithWindows");
            if (_exitMenuItem != null) _exitMenuItem.Text = LocalizationService.Get("Exit");
            RefreshMenuState();
        }

        private void ListenForShutdownRequest()
        {
            try
            {
                _shutdownSignal = new TrayShutdownSignal(() =>
                {
                    _logger.LogInformation("QuestLog asked the tray to exit (update or uninstall).");
                    if (_uiContext == null)
                        Exit(null, EventArgs.Empty);
                    else
                        _uiContext.Post(_ => Exit(null, EventArgs.Empty), null);
                });
            }
            catch (Exception ex)
            {
                // Khong nghe duoc thi trinh cap nhat se giet cung tray; tray van chay binh thuong.
                _logger.LogWarning(ex, "Could not listen for the shutdown request from QuestLog.");
            }
        }

        /// <summary>Panel "Dich vu nen" trong QuestLog dieu khien tray qua Host -> pipe nay.</summary>
        private void ListenForControlRequests()
        {
            if (_uiContext == null)
            {
                _logger.LogWarning("No UI context; QuestLog cannot control the tray.");
                return;
            }

            try
            {
                _controlServer = new TrayControlServer(HandleControlRequestAsync, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not open the tray control pipe.");
            }
        }

        /// <summary>Goi tu luong cua pipe: chuyen sang UI thread nhu mot cu bam menu.</summary>
        private Task<TrayControlResponse> HandleControlRequestAsync(TrayControlRequest request)
        {
            var completion = new TaskCompletionSource<TrayControlResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _uiContext!.Post(async _ =>
            {
                try
                {
                    completion.SetResult(await ExecuteControlRequestAsync(request));
                }
                catch (TrayBusyException ex)
                {
                    completion.SetResult(new TrayControlResponse(false, TrayControlProtocol.TrayBusy, ex.Message, CurrentControlStatus()));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tray control command {Command} failed.", request.Command);
                    completion.SetResult(new TrayControlResponse(false, TrayControlProtocol.CommandFailed, ex.Message, CurrentControlStatus()));
                }
            }, null);
            return completion.Task;
        }

        private async Task<TrayControlResponse> ExecuteControlRequestAsync(TrayControlRequest request)
        {
            if (_isExiting)
                throw new InvalidOperationException("The tray is shutting down.");

            switch (request.Command)
            {
                case TrayControlProtocol.Status:
                    break;

                case TrayControlProtocol.SetService when request.Enabled is bool enabled:
                    if (request.Service == TrayControlProtocol.Playtime && _playTimeMenuItem != null)
                        await SetServiceAsync(_playTimeMenuItem, "PlayTime tracking", enabled, _playTimeService.Start, _playTimeService.Stop);
                    else if (request.Service == TrayControlProtocol.Scanner && _libraryMenuItem != null)
                        await SetServiceAsync(_libraryMenuItem, "Library scanner", enabled, _libraryService.Start, _libraryService.Stop);
                    else
                        return new TrayControlResponse(false, TrayControlProtocol.BadRequest, $"Unknown service '{request.Service}'.");
                    break;

                case TrayControlProtocol.ScanNow:
                    if (!_libraryService.RequestScanNow())
                        return new TrayControlResponse(false, TrayControlProtocol.ScannerStopped, "The library scanner is turned off.", CurrentControlStatus());
                    _logger.LogInformation("Manual library scan requested from QuestLog.");
                    break;

                case TrayControlProtocol.OpenLogs:
                    OpenLogFolderCore();
                    break;

                default:
                    return new TrayControlResponse(false, TrayControlProtocol.BadRequest, $"Unknown command '{request.Command}'.");
            }

            return new TrayControlResponse(true, Status: CurrentControlStatus());
        }

        private TrayControlStatus CurrentControlStatus() => new(
            [
                new TrayServiceState(TrayControlProtocol.Playtime, _playTimeMenuItem?.Checked == true),
                new TrayServiceState(TrayControlProtocol.Scanner, _libraryMenuItem?.Checked == true),
            ],
            StartupHelper.IsStartupEnabled());

        private async void Exit(object? sender, EventArgs e)
        {
            if (_isExiting)
                return;

            _isExiting = true;
            _logger.LogInformation("Shutting down...");
            _contextMenu?.Close();
            if (_contextMenu != null)
                _contextMenu.Enabled = false;
            _trayIcon.Visible = false;

            try
            {
                // Task.Run: Stop() cua cac service cho worker ket thuc, khong duoc cho
                // tren UI thread.
                var shutdown = Task.Run(() =>
                {
                    _libraryService.Stop();
                    _playTimeService.Stop();
                });

                if (await Task.WhenAny(shutdown, Task.Delay(ShutdownTimeout)) != shutdown)
                    _logger.LogWarning("Tray services did not stop within {Timeout}; exiting anyway.", ShutdownTimeout);
                else
                    await shutdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping tray services.");
            }
            finally
            {
                _logger.LogInformation("Tray application stopped.");
                ExitThread();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_languageGate)
                {
                    _languageReloadTimer?.Dispose();
                    _languageReloadTimer = null;
                }

                _configWatcher?.Dispose();
                _shutdownSignal?.Dispose();
                _controlServer?.Dispose();
                _libraryService.GameDiscovered -= OnGameDiscovered;

                foreach (var (item, _, _) in _glyphItems)
                {
                    var image = item.Image;
                    item.Image = null;
                    image?.Dispose();
                }

                _headerFont?.Dispose();
                _statusFont?.Dispose();
                _serviceFont?.Dispose();
                _menuFont?.Dispose();
                _contextMenu?.Dispose();
                _trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr windowHandle, int command);
    }
}
