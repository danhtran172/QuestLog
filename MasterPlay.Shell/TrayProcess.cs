using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MasterPlay.Shell;

/// <summary>
/// Tray la tien trinh rieng, song tiep sau khi Shell dong (dem gio choi). Cac ten
/// duoi day phai khop voi MasterPlay.TrayApp (Program.cs, TrayShutdownSignal.cs,
/// Startup/AutoStartupHelper.cs).
/// </summary>
internal static class TrayProcess
{
    private const string ProcessName = "MasterPlay.TrayApp";
    private const string SingleInstanceMutexName = "QuestLogTraySingleInstance";
    private const string ShutdownEventName = "QuestLogTrayShutdownRequest";
    private const string AutoStartValueName = "MasterPlayTray";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private static readonly string TrayExecutable =
        Path.Combine(AppContext.BaseDirectory, "tray", ProcessName + ".exe");

    public static void EnsureRunning()
    {
        if (Mutex.TryOpenExisting(SingleInstanceMutexName, out var existing))
        {
            existing.Dispose();
            return;
        }

        if (!File.Exists(TrayExecutable))
        {
            ShellLog.Write($"Khong thay tray: {TrayExecutable}");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(TrayExecutable)
            {
                WorkingDirectory = Path.GetDirectoryName(TrayExecutable)!,
                UseShellExecute = false,
            };
            // Token guest chi danh cho WebView2 cua Shell, tray khong can.
            startInfo.Environment.Remove("QUESTLOG_DESKTOP_SESSION_TOKEN");
            Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            ShellLog.Write($"Khong khoi dong duoc tray: {ex}");
        }
    }

    /// <summary>
    /// Yeu cau tray thoat em (ghi not giay choi dang dem) truoc khi thay file khi
    /// cap nhat/go cai dat. Het thoi gian thi Velopack se giet cung.
    /// </summary>
    public static void RequestShutdown(TimeSpan timeout)
    {
        var running = Process.GetProcessesByName(ProcessName);
        try
        {
            if (running.Length == 0) return;

            if (EventWaitHandle.TryOpenExisting(ShutdownEventName, out var shutdown))
            {
                using (shutdown) shutdown.Set();
            }
            else
            {
                // Tray ban cu chua biet tin hieu nay: de Velopack giet cung.
                ShellLog.Write("Tray dang chay nhung khong nghe tin hieu thoat.");
                return;
            }

            var deadline = DateTime.UtcNow + timeout;
            foreach (var process in running)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !process.WaitForExit(remaining))
                {
                    ShellLog.Write("Tray chua thoat kip truoc khi cap nhat.");
                    return;
                }
            }
        }
        finally
        {
            foreach (var process in running) process.Dispose();
        }
    }

    /// <summary>
    /// Cai moi: tray khoi dong cung Windows de dem gio choi ca khi chua mo QuestLog.
    /// Ghi dung dinh dang cua StartupHelper ben tray de muc menu "Start with Windows"
    /// hien dung trang thai. Chi goi luc cai, KHONG goi luc cap nhat: nguoi dung da
    /// tat thi ban cap nhat khong duoc bat lai.
    /// </summary>
    public static void EnableAutoStart()
    {
        if (!File.Exists(TrayExecutable))
        {
            ShellLog.Write($"Khong thay tray de dang ky tu khoi dong: {TrayExecutable}");
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(AutoStartValueName, $"\"{TrayExecutable}\"", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            ShellLog.Write($"Khong dang ky duoc tray tu khoi dong cung Windows: {ex.Message}");
        }
    }

    public static void RemoveAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            // Chi xoa khi muc nay tro vao chinh ban cai nay, khong dung toi tray ban dev.
            if (key?.GetValue(AutoStartValueName) is string command &&
                command.Contains(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue(AutoStartValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            ShellLog.Write($"Khong xoa duoc muc tu khoi dong cua tray: {ex.Message}");
        }
    }
}
