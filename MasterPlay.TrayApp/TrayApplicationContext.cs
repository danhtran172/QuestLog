using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Integrations.Cloudinary;
using MasterPlay.TrayApp.Services;
using MasterPlay.TrayApp.Startup;
using MasterPlay.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        private readonly SynchronizationContext? _uiContext;

        private ToolStripMenuItem? _playTimeMenuItem;
        private ToolStripMenuItem? _libraryMenuItem;
        private ToolStripMenuItem? _backupMenuItem;
        private ToolStripMenuItem? _syncImagesMenuItem;
        private ToolStripMenuItem? _openMasterPlayMenuItem;
        private ToolStripMenuItem? _openLogMenuItem;
        private ToolStripMenuItem? _startupMenuItem;
        private ToolStripMenuItem? _exitMenuItem;
        private ContextMenuStrip? _contextMenu;
        private FileSystemWatcher? _configWatcher;
        private System.Threading.Timer? _languageReloadTimer;

        private bool _isSyncingImages = false;
        private bool _isExiting;

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
                Text = "Questory Tray",
                Visible = true
            };

            BuildContextMenu();
            _uiContext = SynchronizationContext.Current;
            WatchLanguageChanges();

            _playTimeService.Start();
            _libraryService.GameDiscovered += OnGameDiscovered;
            _libraryService.Start();
            _backupService.Start();

            _logger.LogInformation("Tray application started.");
        }

        private void OnGameDiscovered(object? sender, LibraryGameNotification notification)
        {
            void ShowNotification()
            {
                var localizationKey = notification.Kind == LibraryGameNotificationKind.Added
                    ? "NewGameAdded"
                    : "NewGameDetected";
                _trayIcon.ShowBalloonTip(
                    5000,
                    "Questory",
                    LocalizationService.Get(localizationKey, notification.GameName),
                    ToolTipIcon.Info);
            }

            if (_uiContext == null)
                ShowNotification();
            else
                _uiContext.Post(_ => ShowNotification(), null);
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            _openMasterPlayMenuItem = new ToolStripMenuItem(LocalizationService.Get("OpenMasterPlay"), null, OpenMasterPlay);
            _contextMenu.Items.Add(_openMasterPlayMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _playTimeMenuItem = new ToolStripMenuItem(LocalizationService.Get("PlayTimeTracking"), null, TogglePlayTime) { Checked = true };
            _libraryMenuItem = new ToolStripMenuItem(LocalizationService.Get("LibraryScanner"), null, ToggleLibrary) { Checked = true };
            _backupMenuItem = new ToolStripMenuItem(LocalizationService.Get("BackupService"), null, ToggleBackup) { Checked = true };

            _contextMenu.Items.Add(_playTimeMenuItem);
            _contextMenu.Items.Add(_libraryMenuItem);
            _contextMenu.Items.Add(_backupMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _syncImagesMenuItem = new ToolStripMenuItem(LocalizationService.Get("SyncImagesCloudinary"), null, SyncImagesToCloudinary_Click);
            _contextMenu.Items.Add(_syncImagesMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _openLogMenuItem = new ToolStripMenuItem(LocalizationService.Get("OpenLogFolder"), null, OpenLogFolder);
            _startupMenuItem = new ToolStripMenuItem(LocalizationService.Get("StartWithWindows"), null, ToggleStartWithWindows)
            {
                Checked = StartupHelper.IsStartupEnabled(),
                CheckOnClick = false
            };
            _exitMenuItem = new ToolStripMenuItem(LocalizationService.Get("Exit"), null, Exit);
            _contextMenu.Items.Add(_openLogMenuItem);
            _contextMenu.Items.Add(_startupMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(_exitMenuItem);

            _trayIcon.ContextMenuStrip = _contextMenu;
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

        private async void SyncImagesToCloudinary_Click(object? sender, EventArgs e)
        {
            if (_isSyncingImages)
            {
                _trayIcon.ShowBalloonTip(3000, "Questory", LocalizationService.Get("ImageSyncAlreadyRunning"), ToolTipIcon.Info);
                return;
            }

            _isSyncingImages = true;
            _syncImagesMenuItem!.Enabled = false;
            _syncImagesMenuItem.Text = LocalizationService.Get("SyncingImages");

            _logger.LogInformation("Cloudinary image sync started from tray menu.");
            _trayIcon.ShowBalloonTip(3000, "Questory", LocalizationService.Get("StartingImageSync"), ToolTipIcon.Info);

            try
            {
                var progress = new Progress<string>(msg => _logger.LogInformation("[ImageSync] {msg}", msg));
                var result = await CloudinaryImageService.MigrateAllGameImagesToCloudinaryAsync(progress);

                string summary = LocalizationService.Get(
                    "ImageSyncSummary",
                    result.CoverUploaded,
                    result.CoverAlreadySynced,
                    result.CoverMissing,
                    result.GalleryUploaded,
                    result.GalleryAlreadySynced,
                    result.GalleryMissing,
                    result.SteamLibraryUploaded,
                    result.SteamLibraryAlreadySynced,
                    result.SteamLibraryMissing,
                    result.Failed,
                    result.TotalGames);

                _logger.LogInformation("Cloudinary image sync finished. {summary}", summary.Replace("\n", " "));
                _trayIcon.ShowBalloonTip(6000, LocalizationService.Get("ImageSyncComplete"), summary, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Cloudinary image sync.");
                _trayIcon.ShowBalloonTip(5000, LocalizationService.Get("ImageSyncError"), ex.Message, ToolTipIcon.Error);
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
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logPath);
            Process.Start("explorer.exe", logPath);
        }

        private void OpenMasterPlay(object? sender, EventArgs e)
        {
            try
            {
                using var runningProcess = Process.GetProcessesByName("MasterPlay.UI")
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
                _logger.LogError(ex, "Could not open Questory UI.");
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
                Path.Combine(baseDirectory, "MasterPlay.UI.exe"),
                Path.Combine(baseDirectory, "UI", "MasterPlay.UI.exe")
            };

            var outputDirectory = new DirectoryInfo(baseDirectory);
            var configurationDirectory = outputDirectory.Parent;
            var repositoryDirectory = outputDirectory.Parent?.Parent?.Parent?.Parent;
            if (configurationDirectory != null && repositoryDirectory != null)
            {
                candidates.Add(Path.Combine(
                    repositoryDirectory.FullName,
                    "MasterPlay.UI",
                    "bin",
                    configurationDirectory.Name,
                    "net8.0-windows",
                    "MasterPlay.UI.exe"));
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

            if (!succeeded)
            {
                MessageBox.Show(
                    LocalizationService.Get("StartupSettingFailed"),
                    LocalizationService.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(
                LocalizationService.Get(enable ? "WillStartWithWindows" : "WillNotStartWithWindows"),
                LocalizationService.Get("Info"));
        }

        private void WatchLanguageChanges()
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
            _configWatcher.Changed += (_, _) =>
            {
                _languageReloadTimer?.Dispose();
                _languageReloadTimer = new System.Threading.Timer(_ => ReloadLanguage(), null, 500, Timeout.Infinite);
            };
        }

        private void ReloadLanguage()
        {
            if (_contextMenu == null || _contextMenu.IsDisposed)
                return;

            try
            {
                var language = JsonService.LoadConfig().Language;
                _contextMenu.BeginInvoke(() =>
                {
                    LocalizationService.SetLanguage(language);
                    ApplyLocalizedMenuText();
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reload tray language.");
            }
        }

        private void ApplyLocalizedMenuText()
        {
            if (_openMasterPlayMenuItem != null) _openMasterPlayMenuItem.Text = LocalizationService.Get("OpenMasterPlay");
            if (_playTimeMenuItem != null) _playTimeMenuItem.Text = LocalizationService.Get("PlayTimeTracking");
            if (_libraryMenuItem != null) _libraryMenuItem.Text = LocalizationService.Get("LibraryScanner");
            if (_backupMenuItem != null) _backupMenuItem.Text = LocalizationService.Get("BackupService");
            if (_syncImagesMenuItem != null && !_isSyncingImages) _syncImagesMenuItem.Text = LocalizationService.Get("SyncImagesCloudinary");
            if (_openLogMenuItem != null) _openLogMenuItem.Text = LocalizationService.Get("OpenLogFolder");
            if (_startupMenuItem != null) _startupMenuItem.Text = LocalizationService.Get("StartWithWindows");
            if (_exitMenuItem != null) _exitMenuItem.Text = LocalizationService.Get("Exit");
        }

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
                await Task.Run(() =>
                {
                    _backupService.Stop();
                    _libraryService.Stop();
                    _playTimeService.Stop();
                });
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
                _languageReloadTimer?.Dispose();
                _configWatcher?.Dispose();
                _libraryService.GameDiscovered -= OnGameDiscovered;
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
