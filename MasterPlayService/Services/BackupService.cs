using MasterPlay.Helpers;
using MasterPlay.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace MasterPlay.Service.Services
{
    public class BackupService
    {
        private readonly ILogger<BackupService> _logger;
        private System.Timers.Timer? _primaryBackupTimer;
        private bool _isBackupRunning = false;

        public BackupService(ILogger<BackupService> logger)
        {
            _logger = logger;
        }

        public void Start(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BACKUP SERVICE STARTED");
            _primaryBackupTimer = new System.Timers.Timer(60000); // 1 phút
            _primaryBackupTimer.Elapsed += (s, e) => PerformBackup();
            _primaryBackupTimer.AutoReset = true;
            _primaryBackupTimer.Start();
        }

        public void PerformBackup()
        {
            if (_isBackupRunning)
            {
                _logger.LogWarning("Backup skipped: Previous session still active.");
                return;
            }

            try
            {
                _isBackupRunning = true;
                var config = DataService.LoadConfig();
                var games = DataService.LoadGames();

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

                    string processName = game.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    Process[] processes = Process.GetProcessesByName(processName);

                    bool shouldBackup = false;

                    if (processes.Length > 0)
                    {
                        // Game đang chạy → backup luôn
                        shouldBackup = true;
                        _logger.LogInformation("Game {name} is running → perform backup", game.Name);
                    }
                    else
                    {
                        // Game không chạy → kiểm tra process gần nhất có tắt trong 1 phút không
                        DateTime now = DateTime.UtcNow;
                        bool recentProcess = false;

                        // Tìm process có tên tương tự (để tránh miss nếu tên process thay đổi nhẹ)
                        foreach (var proc in Process.GetProcesses())
                        {
                            if (proc.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    DateTime startTime = proc.StartTime.ToUniversalTime();
                                    if ((now - startTime).TotalMinutes < 2)
                                    {
                                        recentProcess = true;
                                        break;
                                    }
                                }
                                catch { /* Bỏ qua process không truy cập được */ }
                            }
                        }

                        if (recentProcess)
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

                    string backupFolder = Path.Combine(config.BackupRootPath, game.Name);
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

        public void Stop()
        {
            _primaryBackupTimer?.Stop();
            _primaryBackupTimer?.Dispose();
            _logger.LogInformation("Backup engine stopped at: {time}", DateTimeOffset.Now);
        }
    }
}