using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Core.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MasterPlay.Infrastructure.Configuration
{
    public static class JsonService
    {
        private static readonly object _fileLock = new object();

        /// <summary>Thu muc du lieu — xem <see cref="AppPaths"/> ve viec di doi ten cu.</summary>
        public static string GetConfigDirectory() => AppPaths.GetDataDirectory();

        public static string GetConfigPath() => Path.Combine(GetConfigDirectory(), AppPaths.ConfigFileName);

        public static List<string> GetGameLibraryPath()
        {
            var config = LoadConfig();
            if (config.GameLibraryPath == null || !config.GameLibraryPath.Any())
            {

                config.GameLibraryPath = new List<string> {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Games")
                };
                SaveConfig(config);
            }
            return config.GameLibraryPath;
        }

        public static string GetDatabasePath()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.DatabasePath))
            {
                config.DatabasePath = AppPaths.GetDatabasePath();
                SaveConfig(config);
                return config.DatabasePath;
            }

            // Duong dan da luu tu ban cu con tro vao thu muc MasterPlay -> keo sang ten moi.
            string rebased = AppPaths.Rebase(config.DatabasePath)!;
            if (!string.Equals(rebased, config.DatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                config.DatabasePath = rebased;
                SaveConfig(config);
            }
            return config.DatabasePath;
        }

        public static string GetBackupRootPath()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.BackupRootPath))
            {
                config.BackupRootPath = Path.Combine(GetConfigDirectory(), "Backups");
                SaveConfig(config);
                return config.BackupRootPath;
            }

            string rebased = AppPaths.Rebase(config.BackupRootPath)!;
            if (!string.Equals(rebased, config.BackupRootPath, StringComparison.OrdinalIgnoreCase))
            {
                config.BackupRootPath = rebased;
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
                        BackupRootPath = Path.Combine(GetConfigDirectory(), "Backups"),
                        DatabasePath = AppPaths.GetDatabasePath(),
                        GameLibraryPath = new List<string> {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Games")
                        }
                    };
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    return defaultConfig;
                }
                var json = File.ReadAllText(configPath);
                var payload = JObject.Parse(json);
                var themeModeProperty = payload.Properties()
                    .FirstOrDefault(property => property.Name.Equals(nameof(Config.ThemeMode), StringComparison.OrdinalIgnoreCase));
                var legacyDarkModeProperty = payload.Properties()
                    .FirstOrDefault(property => property.Name.Equals("isDarkMode", StringComparison.OrdinalIgnoreCase));

                // Migrate the legacy bool once, then write only ThemeMode from now on.
                // A file with neither (the setup window writes only Language) takes the
                // Config default instead of being treated as a legacy light theme.
                if (themeModeProperty is null)
                {
                    payload[nameof(Config.ThemeMode)] = legacyDarkModeProperty is null
                        ? new Config().ThemeMode
                        : legacyDarkModeProperty.Value.Value<bool>() ? "dark" : "light";
                }
                legacyDarkModeProperty?.Remove();

                var config = payload.ToObject<Config>() ?? new Config();
                config.ThemeMode = NormalizeThemeMode(config.ThemeMode);

                if (themeModeProperty is null || legacyDarkModeProperty is not null)
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

                return config;
            }
        }

        private static string NormalizeThemeMode(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "light" => "light",
                "dark" => "dark",
                "jungle" => "jungle",
                _ => "system"
            };

        public static void SaveConfig(Config config)
        {
            lock (_fileLock)
            {
                File.WriteAllText(GetConfigPath(), JsonConvert.SerializeObject(config, Formatting.Indented));
            }
        }
    }
}
