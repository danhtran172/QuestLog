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
            contextMenu.Items.Add(new ToolStripMenuItem("Start with Windows", null, ToggleStartWithWindows));
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

        private void ToggleStartWithWindows(object? sender, EventArgs e)
        {
            string appName = "MasterPlayTray";
            string appPath = Application.ExecutablePath;
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key!.GetValue(appName) == null)
            {
                key.SetValue(appName, appPath);
                MessageBox.Show("Will start with Windows.", "Info");
            }
            else
            {
                key.DeleteValue(appName);
                MessageBox.Show("Will NOT start with Windows.", "Info");
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