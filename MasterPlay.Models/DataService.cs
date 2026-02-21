using MasterPlay.Helpers;
using MasterPlay.Models.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MasterPlay.Models
{
    public static class DataService
    {
        private static readonly object _fileLock = new object();

        #region PATH HELPERS
        public static string GetConfigDirectory() => Path.Combine(@"D:\Apps\[AppData]", "MasterPlay");
        public static string GetConfigPath() => Path.Combine(GetConfigDirectory(), "config.json");

        public static List<string> GetGameLibraryPath()
        {
            var config = LoadConfig();
            if (config.GameLibraryPath == null || !config.GameLibraryPath.Any())
            {
                config.GameLibraryPath = new List<string> { Path.Combine(GetConfigDirectory(), "gellery_data.json") };
                SaveConfig(config);
            }
            return config.GameLibraryPath;
        }
        public static string GetSelectionPath()
        {
            var config = LoadConfig();
            if (config.SelectionPath == null || !config.SelectionPath.Any())
            {
                config.SelectionPath = Path.Combine(GetConfigDirectory(), "selections");
                SaveConfig(config);
            }
            return config.SelectionPath;
        }

        public static string GetBackupRootPath()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.BackupRootPath))
            {
                config.BackupRootPath = Path.Combine(GetConfigDirectory(), "BackUp");
                SaveConfig(config);
            }
            return config.BackupRootPath;
        }

        public static string GetGameDataPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.DataFilePath)) return config.DataFilePath;
            return Path.Combine(GetConfigDirectory(), "gameData.json");
        }

        public static string GetAvailableGamesPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.AvailableGamesPath)) return config.AvailableGamesPath;
            return Path.Combine(GetConfigDirectory(), "availableGames.json");
        }

        public static string GetMetaDataPath()
        {
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.MetaDataPath)) return config.MetaDataPath;
            return Path.Combine(GetConfigDirectory(), "game_metadata.json");
        }
        #endregion

        #region CONFIG (CORE IO)
        public static Config LoadConfig()
        {
            lock (_fileLock)
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
                        BackupRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Backups"),
                        GameLibraryPath = new List<string> { @"D:\Games" },
                        AvailableGamesPath = Path.Combine(dir, "availableGames.json")
                    };
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    return defaultConfig;
                }
                return JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath)) ?? new Config();
            }
        }

        public static void SaveConfig(Config config)
        {
            lock (_fileLock)
            {
                File.WriteAllText(GetConfigPath(), JsonConvert.SerializeObject(config, Formatting.Indented));
            }
        }
        #endregion

        #region GAMES DATA (CORE IO)
        public static List<Game> LoadGames()
        {
            string path = GetGameDataPath();
            lock (_fileLock)
            {
                if (!File.Exists(path)) return new List<Game>();
                string content = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<Game>>(content) ?? new List<Game>();
            }
        }

        public static void SaveGamesData(List<Game> games)
        {
            if (games == null) return;
            string path = GetGameDataPath();
            lock (_fileLock)
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(games, Formatting.Indented));
            }
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
        #endregion

        #region AVAILABLE GAMES (CORE IO)
        public static List<DetectedProcess> LoadDetectedProcesses()
        {
            string path = GetAvailableGamesPath();
            lock (_fileLock)
            {
                if (!File.Exists(path)) return new List<DetectedProcess>();
                return JsonConvert.DeserializeObject<List<DetectedProcess>>(File.ReadAllText(path)) ?? new List<DetectedProcess>();
            }
        }

        public static void SaveAvailableGames(List<DetectedProcess> games)
        {
            string path = GetAvailableGamesPath();
            lock (_fileLock)
            {
                if (string.IsNullOrEmpty(path)) return;
                File.WriteAllText(path, JsonConvert.SerializeObject(games, Formatting.Indented));
            }
        }
        #endregion

        #region SYSTEM LOGIC
        public static void SortGames()
        {
            var games = LoadGames();
            if (games == null || games.Count == 0) return;

            var sorted = games
                .OrderByDescending(g => g.IsDisabled)
                .ThenBy(g => g.Ready)
                .ThenByDescending(g => g.TotalPlayTimeSeconds)
                .ThenBy(g => g.Name)
                .ToList();

            SaveGamesData(sorted);
        }

        public static void SyncReadyGames()
        {
            var games = LoadGames();
            var availables = LoadDetectedProcesses();
            if (games == null || games.Count == 0) return;

            bool dataChanged = false;

            foreach (var game in games)
            {
                try
                {
                    string processName = game.ExeName?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
                        ? game.ExeName : game.ExeName + ".exe";

                    var match = availables.FirstOrDefault(a =>
                        string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase));

                    bool newReady = match != null && FileHelper.FileCheck(match.FullPath);

                    if (game.Ready != newReady)
                    {
                        game.Ready = newReady;
                        dataChanged = true;
                    }
                }
                catch (Exception ex)
                {
                    if (game.Ready)
                    {
                        game.Ready = false;
                        dataChanged = true;
                    }
                }
            }

            // Chỉ save nếu có thay đổi thực sự
            if (dataChanged)
            {
                SaveGamesData(games);
            }
        }
        #endregion

        #region FUZZY SEARCH
        public static async Task<GameMetadata?> FindGameMetaData(string gameName)
        {
            var jsonFilePath = GetMetaDataPath();
            var NullGame = new GameMetadata { Game = "", Exe = "N/A", SaveFolder = "N/A" };

            if (string.IsNullOrWhiteSpace(gameName))
                throw new ArgumentException("Game name cannot be empty", nameof(gameName));

            return await Task.Run(() =>
            {
                lock (_fileLock)
                {
                    if (!File.Exists(jsonFilePath)) return NullGame;
                    try
                    {
                        string jsonContent = File.ReadAllText(jsonFilePath);
                        var gamesList = JsonConvert.DeserializeObject<List<GameMetadata>>(jsonContent);
                        if (gamesList == null || gamesList.Count == 0) return NullGame;

                        var bestMatch = gamesList.FirstOrDefault(x => string.Equals(x.Game, gameName, StringComparison.OrdinalIgnoreCase));
                        return bestMatch ?? NullGame;
                    }
                    catch { return NullGame; }
                }
            });
        }
        #endregion


        public static Selections LoadSelection()
{
    string path = GetSelectionPath();
    lock (_fileLock)
    {
        Selections selections;

        if (!File.Exists(path))
        {
            selections = new Selections();  // hoặc CreateDefault...
        }
        else
        {
            try
            {
                string content = File.ReadAllText(path);
                selections = JsonConvert.DeserializeObject<Selections>(content) ?? new Selections();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading selections: {ex.Message}");
                selections = new Selections();
            }
        }

        return selections;
    }
}
        public static Dictionary<string, string> Progress { get; private set; } = new();
        public static Dictionary<string, string> Ownership { get; private set; } = new();

        public static void AssignDefaultColors()
        {
            Progress = new Dictionary<string, string>
            {
                { "Done", "#6DC3BB" },
                { "Playing", "#5EABD6" },
                { "Nextplay", "#FF8FB7" },
                { "Unfinished", "#AE75DA" },
                { "Intedned", "#E67514" },
                { "Pending", "#D7D7D7" }
            };

            Ownership = new Dictionary<string, string>
            {
                { "Ready", "#A2D5C6" },
                { "Pirated", "#FFAB5B" },
                { "GamePass", "#2DAA9E" },
                { "Not Owned", "#D7D7D7" },
                { "PS Required", "#C62E2E" }
            };
        }

    }
}