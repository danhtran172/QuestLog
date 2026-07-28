using MasterPlay.TrayApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MasterPlay.TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrayApplicationContext> _logger;
        private readonly PlayTimeService _playTimeService;
        private readonly LibraryService _libraryService;
        private readonly BackupService _backupService;

        private ToolStripMenuItem? _playTimeMenuItem;
        private ToolStripMenuItem? _libraryMenuItem;
        private ToolStripMenuItem? _backupMenuItem;
        private ToolStripMenuItem? _startupMenuItem;

        // Dùng riêng cho Tray app. UI app (MainWindow.xaml.cs) phải dùng tên khác
        // để tránh 2 nơi cùng ghi đè lên 1 registry value, gây rối trạng thái.
        private const string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupAppName = "MasterPlayTray";

        public TrayApplicationContext(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<TrayApplicationContext>>();
            _playTimeService = serviceProvider.GetRequiredService<PlayTimeService>();
            _libraryService = serviceProvider.GetRequiredService<LibraryService>();
            _backupService = serviceProvider.GetRequiredService<BackupService>();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "TrayIcon.ico");
            Icon appIcon;
            if (File.Exists(iconPath))
            {
                using var stream = File.OpenRead(iconPath);
                appIcon = new Icon(stream);
            }
            else
            {
                appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)!;
            }

            _trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "MasterPlay Tray",
                Visible = true
            };

            BuildContextMenu();

            _playTimeService.Start();
            _libraryService.Start();
            _backupService.Start();

            _logger.LogInformation("Tray application started.");
        }

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            _playTimeMenuItem = new ToolStripMenuItem("PlayTime Tracking", null, TogglePlayTime) { Checked = true };
            _libraryMenuItem = new ToolStripMenuItem("Library Scanner", null, ToggleLibrary) { Checked = true };
            _backupMenuItem = new ToolStripMenuItem("Backup Service", null, ToggleBackup) { Checked = true };

            contextMenu.Items.Add(_playTimeMenuItem);
            contextMenu.Items.Add(_libraryMenuItem);
            contextMenu.Items.Add(_backupMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(new ToolStripMenuItem("Open Log Folder", null, OpenLogFolder));

            // Khởi tạo đúng trạng thái Checked ngay từ đầu, dựa trên việc kiểm tra
            // registry entry hiện có có thật sự trỏ đúng tới exe hiện tại hay không
            // (trước đây không hề set Checked nên ô này không bao giờ hiện tick).
            _startupMenuItem = new ToolStripMenuItem("Start with Windows", null, ToggleStartWithWindows)
            {
                Checked = IsStartupEnabled()
            };
            contextMenu.Items.Add(_startupMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, Exit));

            _trayIcon.ContextMenuStrip = contextMenu;
        }

        private void TogglePlayTime(object? sender, EventArgs e)
        {
            if (_playTimeMenuItem!.Checked)
            {
                _playTimeService.Stop();
                _playTimeMenuItem.Checked = false;
                _logger.LogInformation("PlayTime tracking disabled.");
            }
            else
            {
                _playTimeService.Start();
                _playTimeMenuItem.Checked = true;
                _logger.LogInformation("PlayTime tracking enabled.");
            }
        }

        private void ToggleLibrary(object? sender, EventArgs e)
        {
            if (_libraryMenuItem!.Checked)
            {
                _libraryService.Stop();
                _libraryMenuItem.Checked = false;
                _logger.LogInformation("Library scanner disabled.");
            }
            else
            {
                _libraryService.Start();
                _libraryMenuItem.Checked = true;
                _logger.LogInformation("Library scanner enabled.");
            }
        }

        private void ToggleBackup(object? sender, EventArgs e)
        {
            if (_backupMenuItem!.Checked)
            {
                _backupService.Stop();
                _backupMenuItem.Checked = false;
                _logger.LogInformation("Backup service disabled.");
            }
            else
            {
                _backupService.Start();
                _backupMenuItem.Checked = true;
                _logger.LogInformation("Backup service enabled.");
            }
        }

        private void OpenLogFolder(object? sender, EventArgs e)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logPath);
            Process.Start("explorer.exe", logPath);
        }

        // Kiểm tra registry KHÔNG chỉ dựa vào "key có tồn tại hay không", mà còn so
        // sánh đường dẫn lưu trong đó có đúng bằng đường dẫn exe hiện tại không.
        // Nếu trước đây app từng được publish/di chuyển sang thư mục khác, entry cũ
        // trong registry có thể đang trỏ tới 1 file exe không còn tồn tại nữa -
        // trường hợp đó phải coi là "chưa thật sự bật", không phải "đang bật".
        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryPath, false);
                var value = key?.GetValue(StartupAppName) as string;
                return !string.IsNullOrEmpty(value) &&
                       string.Equals(value.Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void ToggleStartWithWindows(object? sender, EventArgs e)
        {
            try
            {
                string appPath = Application.ExecutablePath;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true);
                if (key == null)
                {
                    MessageBox.Show("Cannot access the Windows startup registry key.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                bool currentlyEnabled = IsStartupEnabled();

                if (!currentlyEnabled)
                {
                    // Luôn ghi đè, kể cả khi key cũ đã tồn tại nhưng trỏ sai đường dẫn -
                    // nhờ đó tự sửa luôn các entry bị lệch do publish/di chuyển thư mục
                    // trước đây, thay vì bị hiểu nhầm là "đã bật" rồi xóa mất nó đi.
                    key.SetValue(StartupAppName, appPath);
                    _startupMenuItem!.Checked = true;
                    _logger.LogInformation("Start with Windows enabled. Path: {path}", appPath);
                    MessageBox.Show("Will start with Windows.", "Info");
                }
                else
                {
                    key.DeleteValue(StartupAppName, false);
                    _startupMenuItem!.Checked = false;
                    _logger.LogInformation("Start with Windows disabled.");
                    MessageBox.Show("Will NOT start with Windows.", "Info");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling Start with Windows.");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Exit(object? sender, EventArgs e)
        {
            _logger.LogInformation("Shutting down...");
            _playTimeService.Stop();
            _libraryService.Stop();
            _backupService.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _trayIcon?.Dispose();
            base.Dispose(disposing);
        }
    }
}