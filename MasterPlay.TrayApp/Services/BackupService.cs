using MasterPlay.Helpers;
using MasterPlay.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MasterPlay.TrayApp.Services
{
    // BackupService
    public class BackupService
    {
        // Fields
        private readonly ILogger<BackupService> _logger;
        private System.Timers.Timer? _primaryBackupTimer;
        private bool _isBackupRunning = false;

        // Constructor
        public BackupService(ILogger<BackupService> logger)
        {
            _logger = logger;
        }

        // Public Methods
        public void Start()
        {
            _logger.LogInformation("BACKUP SERVICE STARTED");
            _primaryBackupTimer = new System.Timers.Timer(60000);
            _primaryBackupTimer.Elapsed += (s, e) => PerformBackup();
            _primaryBackupTimer.AutoReset = true;
            _primaryBackupTimer.Start();
        }

        public void Stop()
        {
            _primaryBackupTimer?.Stop();
            _primaryBackupTimer?.Dispose();
            _logger.LogInformation("Backup engine stopped.");
        }

        // Private Methods
        private void PerformBackup()
        {
            if (_isBackupRunning)
            {
                _logger.LogWarning("Backup skipped: Previous session still active.");
                return;
            }

            try
            {
                _isBackupRunning = true;
                var config = JsonService.LoadConfig();
                var games = DataService.LoadActiveGames();

                if (string.IsNullOrEmpty(config.BackupRootPath))
                {
                    _logger.LogWarning("Backup skipped: BackupRootPath is empty.");
                    return;
                }

                foreach (var game in games)
                {
                    if (string.IsNullOrEmpty(game.ExeName) || string.IsNullOrEmpty(game.SaveFolderPath))
                    {
                        continue;
                    }

                    if (!Directory.Exists(game.SaveFolderPath))
                    {
                        _logger.LogWarning("Save folder not found for game {name}: {path}", game.Name, game.SaveFolderPath);
                        continue;
                    }

                    string processName = game.ExeName;
                    Process[] processes = Process.GetProcessesByName(processName);

                    bool shouldBackup = false;

                    if (processes.Length > 0)
                    {
                        shouldBackup = true;
                        _logger.LogInformation("Game {name} is running → perform backup", game.Name);
                    }
                    else
                    {
                        DateTime now = DateTime.UtcNow;
                        bool recentPlay = false;

                        foreach (var proc in Process.GetProcesses())
                        {
                            if (proc.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    DateTime startTime = proc.StartTime.ToUniversalTime();
                                    if ((now - startTime).TotalMinutes < 2)
                                    {
                                        recentPlay = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (recentPlay)
                        {
                            shouldBackup = true;
                            _logger.LogInformation("Game {name} just stopped within 1 minute → perform backup", game.Name);
                        }
                    }

                    if (!shouldBackup)
                    {
                        _logger.LogInformation("Skipped backup for {name}: not running and no recent activity", game.Name);
                        continue;
                    }

                    _logger.LogInformation("Starting backup for game: {name}", game.Name);

                    string backupFolder = game.BackupFolderPath;
                    Directory.CreateDirectory(backupFolder);

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string backupSubFolder = Path.Combine(backupFolder, $"Backup_{timestamp}");
                    Directory.CreateDirectory(backupSubFolder);

                    CopyDirectory(game.SaveFolderPath, backupSubFolder);

                    _logger.LogInformation("Backup successful for {name} at {path}", game.Name, backupSubFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup process.");
            }
            finally
            {
                _isBackupRunning = false;
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}