using MasterPlay.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
            _logger.LogInformation("Backup engine initialized at: {time}", DateTimeOffset.Now);

            _primaryBackupTimer = new System.Timers.Timer(60000); // 1 min
            _primaryBackupTimer.Elapsed += (s, e) => PerformBackup("save_backup.dat");
            _primaryBackupTimer.AutoReset = true;
            _primaryBackupTimer.Start();
        }

public void PerformBackup(string backupFileName)
    {
        if (_isBackupRunning)
        {
            _logger.LogWarning("Backup skipped: Previous session still active.");
            return;
        }

        try
        {
            _isBackupRunning = true;
            var config = ConfigManager.LoadConfig();
            var games = ConfigManager.LoadGames();

            if (string.IsNullOrEmpty(config.BackupRootPath)) return;

            foreach (var game in games)
            {
                // 1. Kiểm tra xem Game có đang chạy hay không
                if (string.IsNullOrEmpty(game.ExeName))
                {
                    _logger.LogWarning("Game {name}: ExeName is not configured. Skipping...", game.Name);
                    continue;
                }

                // Xử lý tên Exe (bỏ đuôi .exe nếu người dùng lỡ nhập vào config)
                string processName = game.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

                bool isGameRunning = Process.GetProcessesByName(processName).Length > 0;

                //if (!isGameRunning)
                //{
                //    _logger.LogDebug("Game {name} ({exe}.exe) is not running. Backup skipped.", game.Name, processName );
                //    continue;
                //}

                // 2. Nếu game đang chạy, kiểm tra file save có tồn tại không
                if (string.IsNullOrEmpty(game.SaveFolderPath) || !File.Exists(game.SaveFolderPath))
                {
                    _logger.LogWarning("Game {name}: Running but save folder: {path} do not exist", game.Name, game.SaveFolderPath);
                    continue;
                }

                // 3. Thực hiện Backup
                _logger.LogInformation("{name} is Running. Starting backup...", game.Name);

                string backupFolder = Path.Combine(config.BackupRootPath, game.Name);
                Directory.CreateDirectory(backupFolder);

                string fullPath = Path.GetFullPath(Path.Combine(backupFolder, backupFileName));

                File.Copy(game.SaveFolderPath, fullPath, true);

                _logger.LogInformation("{name}: Backup successful! in {path}", game.Name, fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during selective backup process.");
        }
        finally
        {
            _isBackupRunning = false;
        }
    }

    public void Stop()
        {
            _primaryBackupTimer?.Stop();
            _logger.LogInformation("Backup engine stopped at: {time}", DateTimeOffset.Now);
        }
    }
}