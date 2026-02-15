using MasterPlay.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        // PATH helpers
        public static string GetConfigDirectory()
        {
            return Path.Combine(@"D:\Apps\[AppData]", "MasterPlay");
        }

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

        public static string GetGameDataPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.DataFilePath))
            {
                return config.DataFilePath;
            }

            string fallback = Path.Combine(GetConfigDirectory(), "gameData.json");
            config.DataFilePath = fallback;
            SaveConfig(config);
            return fallback;
        }

        public static string GetAvailableGamesPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.AvailableGamesPath))
            {
                return config.AvailableGamesPath;
            }

            string fallback = Path.Combine(GetConfigDirectory(), "availableGames.json");
            config.AvailableGamesPath = fallback;
            SaveConfig(config);
            return fallback;
        }

        // CONFIG
        public static Config LoadConfig()
        {
            string configPath = GetConfigPath();
            string dir = Path.GetDirectoryName(configPath) ?? GetConfigDirectory();

            Directory.CreateDirectory(dir);

            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    ConfigFilePath = configPath,
                    DataFilePath = Path.Combine(dir, "gameData.json"),
                    BackupRootPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Backups"),
                    GameLibraryPath = @"D:\Games",
                    AvailableGamesPath = Path.Combine(dir, "availableGames.json")
                };
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            string content = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<Config>(content) ?? new Config();
            return config;
        }

        public static void SaveConfig(Config config)
        {
            string path = GetConfigPath();
            config.ConfigFilePath = path;

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        // GAMES
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
            return JsonConvert.DeserializeObject<List<Game>>(content) ?? new List<Game>();
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

        // AVAILABLE GAMES
        public static List<DetectedGame> LoadAvailableGames()
        {
            string path = GetAvailableGamesPath();

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

        // SYSTEM
        public static void SortGames()
        {
            var games = LoadGames();
            games = games
                .OrderByDescending(g => g.Ready)
                .ThenBy(g => g.IsDisabled)
                .ThenByDescending(g => g.TotalPlayTimeSeconds)
                .ThenBy(g => g.Name)
                .ToList();

            SaveGamesData(games);
        }

        public static void SyncReadyGames()
        {
            var games = LoadGames();
            var availables = LoadAvailableGames();

            foreach (var game in games)
            {
                try
                {
                    string processName = game.ExeName?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
                        ? game.ExeName
                        : game.ExeName + ".exe";

                    var match = availables.FirstOrDefault(a =>
                        string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase));

                    game.Ready = match != null && FileHelper.FileCheck(match.FullPath);
                }
                catch
                {
                    game.Ready = false;
                }
            }

            SaveGamesData(games);
        }

        // FUZZY SEARCH
            public static async Task<string?> FindClosestGameExeAsync(
                string gameName,
                double minSimilarity = 0.6)
            {
            var jsonFilePath = "D:\\Apps\\[AppData]\\MasterPlay\\game_metaData.json";


                if (string.IsNullOrWhiteSpace(gameName))
                    throw new ArgumentException("Game name cannot be empty", nameof(gameName));

                if (!File.Exists(jsonFilePath))
                    throw new FileNotFoundException("JSON file not found", jsonFilePath);

                try
                {
                    string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                    var games = JsonConvert.DeserializeObject<List<GameEntry>>(jsonContent);

                    if (games == null || games.Count == 0)
                        return "N/A";

                    var bestMatch = games
                        .Where(x => x.Game.ToLowerInvariant() == gameName.ToLowerInvariant())
                        .FirstOrDefault();

                    return bestMatch?.Exe;
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Invalid JSON format in {jsonFilePath}", ex);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error while searching game '{gameName}': {ex.Message}", ex);
                }
            }

           
        }
    
}