using MasterPlay.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MasterPlay.Service.Services
{
    public class LibraryService : BackgroundService
    {
        private readonly string _availableGamesPath;

        public LibraryService()
        {
            // Đường dẫn availableGames.json cùng thư mục với config
            ;
            _availableGamesPath = ConfigManager.GetConfigData("availableGames.json");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("GameLibraryScannerService started - Scanning GameLibrary every 60 seconds");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanGameLibrary();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GameLibrary scan error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ScanGameLibrary()
        {
            string libraryPath = ConfigManager.GetGameLibraryPath();

            if (!Directory.Exists(libraryPath))
            {
                Console.WriteLine($"GameLibrary folder not found: {libraryPath}");
                return;
            }

            // Lấy tất cả .exe trong thư mục (không đệ quy, chỉ file trực tiếp)
            var exeFiles = Directory.GetFiles(libraryPath, "*.exe", SearchOption.TopDirectoryOnly);

            // Load availableGames.json (nếu chưa có thì tạo rỗng)
            List<AvailableGame> availableGames = LoadAvailableGames();

            // Danh sách exe hiện có trong thư mục
            var currentExeNames = exeFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Thêm file .exe mới vào availableGames
            foreach (string exeFile in exeFiles)
            {
                string exeName = Path.GetFileName(exeFile);
                if (!availableGames.Any(g => string.Equals(g.ExeName, exeName, StringComparison.OrdinalIgnoreCase)))
                {
                    availableGames.Add(new AvailableGame
                    {
                        ExeName = exeName,
                        FullPath = exeFile,
                        AddedTime = DateTime.Now.ToString("o")
                    });

                    Console.WriteLine($"Added new game: {exeName}");
                }
            }

            // 2. Xóa các entry trong availableGames mà file .exe không còn tồn tại
            int removedCount = availableGames.RemoveAll(g => !currentExeNames.Contains(g.ExeName));
            if (removedCount > 0)
            {
                Console.WriteLine($"Removed {removedCount} missing games from availableGames.json");
            }

            // Lưu lại file
            SaveAvailableGames(availableGames);
        }

        private List<AvailableGame> LoadAvailableGames()
        {
            if (!File.Exists(_availableGamesPath))
            {
                return new List<AvailableGame>();
            }

            string json = File.ReadAllText(_availableGamesPath);
            return JsonConvert.DeserializeObject<List<AvailableGame>>(json) ?? new List<AvailableGame>();
        }

        private void SaveAvailableGames(List<AvailableGame> games)
        {
            string json = JsonConvert.SerializeObject(games, Formatting.Indented);
            File.WriteAllText(_availableGamesPath, json);
        }
    }

    // Model đơn giản cho availableGames.json
    public class AvailableGame
    {
        public string ExeName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string AddedTime { get; set; } = string.Empty;
    }
}