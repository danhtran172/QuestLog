using MasterPlay.Helpers;
using MasterPlay.Models.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MasterPlay.Models
{
    public static class JsonService
    {
        private static readonly object _fileLock = new object();

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

        public static string GetDatabasePath()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.DatabasePath))
            {
                config.DatabasePath = Path.Combine(GetConfigDirectory(), "MasterPlay.db");
                SaveConfig(config);
            }
            return config.DatabasePath;
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
                        BackupRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Backups"),
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
    }
}