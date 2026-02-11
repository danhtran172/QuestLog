using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MasterPlay.Models;
using Microsoft.Extensions.Hosting;

namespace MasterPlay.Service.Services
{
    public class BackupService : BackgroundService
    {
        private System.Timers.Timer? _primaryBackupTimer = null;
        private System.Timers.Timer? _secondaryBackupTimer = null;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("BackupService started - Auto Backup mode");

            _primaryBackupTimer = new System.Timers.Timer(60000); // 1 minute
            _primaryBackupTimer.Elapsed += (s, e) => PerformBackup("save_backup.dat");
            _primaryBackupTimer.AutoReset = true;
            _primaryBackupTimer.Start();

            _secondaryBackupTimer = new System.Timers.Timer(600000); // 10 minutes
            _secondaryBackupTimer.Elapsed += (s, e) => PerformBackup("save_backup_1.dat");
            _secondaryBackupTimer.AutoReset = true;
            _secondaryBackupTimer.Start();

            return Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private void PerformBackup(string backupFileName)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                var games = ConfigManager.LoadGames();
                if (string.IsNullOrEmpty(config.BackupRootPath))
                {
                    Console.WriteLine("BackupRootPath not set in config.");
                    return;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting backup: {backupFileName}");

                foreach (var game in games)
                {
                    if (string.IsNullOrEmpty(game.SaveFilePath) || !File.Exists(game.SaveFilePath))
                    {
                        Console.WriteLine($" - Game {game.Name}: Original save file not found → skipped");
                        continue;
                    }

                    string backupFolder = Path.Combine(config.BackupRootPath, game.Name);
                    Directory.CreateDirectory(backupFolder);

                    string backupPath = Path.Combine(backupFolder, backupFileName);

                    try
                    {
                        File.Copy(game.SaveFilePath, backupPath, true);
                        Console.WriteLine($" - Game {game.Name}: Backup successful → {backupPath}");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($" - Game {game.Name}: Backup error (file may be in use) → {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - Game {game.Name}: Unexpected error → {ex.Message}");
                    }
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Backup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Global backup error: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _primaryBackupTimer?.Dispose();
            _secondaryBackupTimer?.Dispose();
            base.Dispose();
            Console.WriteLine("BackupService stopped");
        }
    }
}