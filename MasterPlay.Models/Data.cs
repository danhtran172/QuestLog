using MasterPlay.Helpers;
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

    public static class Data
    {
        //PATH
        public static string GetConfigDirectory()
        {
            return Path.Combine(@"D:\\Apps\\[AppData]", "MasterPlay");
        }

        public static string GetConfigPath()
        {
            return Path.Combine(GetConfigDirectory(), "config.json");
        }

        public static string GetGameLibraryPath()
        {
            string configPath = GetConfigPath(); // an toàn, không vòng lặp
            string dir = Path.GetDirectoryName(configPath) ?? GetConfigDirectory();

            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.GameLibraryPath))
            {
                config.GameLibraryPath = Path.Combine(config.BackupRootPath, "GameLibrary");
                SaveConfig(config);
            }
            return config.GameLibraryPath;
        }

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

            string fallback = Path.Combine(dir, "gameData.json");

            if (string.IsNullOrEmpty(config.DataFilePath))
            {
                config.DataFilePath = fallback;
                SaveConfig(config);
            }

            return fallback;
        }
        public static string GetAvailableGamesPath()
        {
            string configPath = GetConfigPath();
            string dir = Path.GetDirectoryName(configPath) ?? GetConfigDirectory();
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.AvailableGamesPath))
            {
                return config.AvailableGamesPath;
            }

            string fallback = Path.Combine(dir, "availableGames.json");
            if (string.IsNullOrEmpty(config.AvailableGamesPath))
            {
                config.AvailableGamesPath = fallback;
                SaveConfig(config);
            }

            return fallback;
        }
       
        //CONFIG
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
            return loadedConfig;
        }

        public static void SaveConfig(Config config)
        {
            string path = GetConfigPath();  // an toàn
            config.ConfigFilePath = path;
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        //GAMES

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

        public static void SaveGamesData(List<Game> games)
        {
            string path = GetGameDataPath();
            string json = JsonConvert.SerializeObject(games, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        //AVAILABLE GAMES
        public static List<DetectedGame> LoadAvailableGames()
        {
            
            string path = GetAvailableGamesPath();
            Console.WriteLine("path: " + path);
            if (!File.Exists(path))
            {
                return new List<DetectedGame>();
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<DetectedGame>>(json) ?? new List<DetectedGame>();
  
        }

        public static void SaveAvailableGames(List<DetectedGame> games)
        {
            string path = GetAvailableGamesPath();
            if (string.IsNullOrEmpty(path)) return;

            string json = JsonConvert.SerializeObject(games, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        //SYSTEM
        public static void SortGames()
        {
            var games = LoadGames();
            if (games != null)
            {
                games = games
                    .OrderByDescending(g => g.Ready)                // Ready (true > false)
                    .ThenBy(g => g.IsDisabled)                     // IsDisabled (false > true)
                    .ThenByDescending(g => g.TotalPlayTimeSeconds) // Playtime (nhiều > ít)
                    .ThenBy(g => g.Name)                           // Name (A > Z)
                    .ToList();
                SaveGamesData(games);
            }

        }

        public static void SyncReadyGames()
        {
            var games = LoadGames();
            var availables = LoadAvailableGames();

            foreach (var game in games)
            {
                try
                {
                    var processName = game.ExeName + ".exe";
                    var able = availables.Any(a =>
                        string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase));
                    
                    string fullPath = availables.FirstOrDefault(a =>
                        string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase))?.FullPath;


                    game.Ready = able && FileHelper.FileCheck(fullPath);
                    
                }
                catch
                {
                    game.Ready = false;
                }
            }
            SaveGamesData(games);
        }
    }
}