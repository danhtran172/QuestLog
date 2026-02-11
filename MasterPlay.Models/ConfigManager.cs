using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace MasterPlay.Models
{
    public class Config
    {
        public string BackupRootPath { get; set; } = string.Empty;
        public string ConfigFilePath { get; set; } = string.Empty;
        public string DataFilePath { get; set; } = string.Empty;
        public string GameLibraryPath { get; set; } = string.Empty;
        public string AvailableGamesPath { get; set; } = string.Empty;

    }

    public static class ConfigManager
    {
        
        public static string GetConfigDirectory()
        {
            return Path.Combine(@"D:\\Apps\\[AppData]", "MasterPlay");
        }

        // SỬA: Không gọi GetConfigData nữa, tính trực tiếp
        public static string GetConfigPath()
        {
            return Path.Combine(GetConfigDirectory(), "config.json");
        }

        public static string GetGameLibraryPath()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.GameLibraryPath))
            {
                config.GameLibraryPath = Path.Combine(config.BackupRootPath, "GameLibrary");
                SaveConfig(config);
            }
            return config.GameLibraryPath;
        }

        // SỬA: Tính DataFilePath trực tiếp nếu chưa có
        public static string GetGameDataPath()
        {
            string configPath = GetConfigPath(); // an toàn, không vòng lặp
            string dir = Path.GetDirectoryName(configPath) ?? GetConfigDirectory();

            // Nếu config đã load và có DataFilePath → dùng nó
            var config = LoadConfig(); // Load một lần, không đệ quy
            if (!string.IsNullOrEmpty(config.DataFilePath))
            {
                return config.DataFilePath;
            }

            // Fallback: cùng thư mục với config
            string fallback = Path.Combine(dir, "gameData.json");

            // Lưu fallback vào config để lần sau dùng luôn
            if (string.IsNullOrEmpty(config.DataFilePath))
            {
                config.DataFilePath = fallback;
                SaveConfig(config);
            }

            return fallback;
        }

        public static string GetAvailableGamesPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.AvailableGamesPath))
            {
                return config.AvailableGamesPath;
            }
            return null;
        }


        public static Config LoadConfig()
        {
            string dir = GetConfigDirectory();
            string configPath = GetConfigPath();

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            //Nếu ko có file config thì tạo mới default
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    ConfigFilePath = configPath,
                    DataFilePath = Path.Combine(dir, "gameData.json"),
                    BackupRootPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Backups"),
                    GameLibraryPath = "D:\\Games",
                    AvailableGamesPath = Path.Combine(dir, "availableGames.json")
                };

                string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                File.WriteAllText(configPath, json);
                return defaultConfig;
            }

            string content = File.ReadAllText(configPath);
            var loadedConfig = JsonConvert.DeserializeObject<Config>(content) ?? new Config();

    // Fix nếu thiếu field
            //bool changed = false;
            //if (string.IsNullOrEmpty(loadedConfig.ConfigFilePath))
            //{
            //    loadedConfig.ConfigFilePath = configPath;
            //    changed = true;
            //}
            //if (string.IsNullOrEmpty(loadedConfig.DataFilePath))
            //{
            //    loadedConfig.DataFilePath = Path.Combine(dir, "gameData.json");
            //    changed = true;
            //}

            //if (changed)
            //{
            //    SaveConfig(loadedConfig);
            //}

            return loadedConfig;
        }

        public static void SaveConfig(Config config)
        {
            string path = GetConfigPath();  // an toàn
            config.ConfigFilePath = path;
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        // GAME LIST - không thay đổi nhiều
        public static List<Game> LoadGames()
        {
            string path = GetGameDataPath();
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
                File.WriteAllText(path, "[]");
                return new List<Game>();
            }

            string content = File.ReadAllText(path);
            var games = JsonConvert.DeserializeObject<List<Game>>(content);
            return games ?? new List<Game>();
        }

        public static void SaveGamesData(List<Game> games)
        {
            string path = GetGameDataPath();
            string json = JsonConvert.SerializeObject(games, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static List<Game> GetAllGames()
        {
            return LoadGames();
        }

        public static void UpdateGame(Game updatedGame)
        {
            var games = LoadGames();
            int index = games.FindIndex(g => g.Id == updatedGame.Id);
            if (index >= 0)
            {
                games[index] = updatedGame;
                SaveGamesData(games);
            }
        }

        public static List<AvailableGame> LoadAvailableGames()
        {
            
            string path = GetAvailableGamesPath();
            Console.WriteLine("path: " + path);
            if (!File.Exists(path))
            {
                return new List<AvailableGame>();
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<AvailableGame>>(json) ?? new List<AvailableGame>();
        }
    }
}