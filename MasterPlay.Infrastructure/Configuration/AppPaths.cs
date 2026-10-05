using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MasterPlay.Infrastructure.Configuration
{
    /// <summary>
    /// Noi DUY NHAT quyet dinh app ghi du lieu o dau, va la noi chuyen du lieu
    /// cu sang ten moi.
    ///
    /// Luc cai dat, nguoi dung chon thu muc du lieu (xem MasterPlay.Shell/SetupWindow).
    /// Lua chon nam trong <see cref="LocationFilePath"/>; khong co file do thi dung
    /// thu muc mac dinh %APPDATA%\QuestLog. Shell, Host va Tray deu doc qua day nen
    /// ca ba luon thay cung mot config/CSDL. File nay duoc Shell bien dich kem (link
    /// nguon) de khoi phai keo ca Infrastructure vao thu muc cai.
    ///
    /// App doi ten tu MasterPlay/Questory sang QuestLog. Thu muc du lieu that
    /// (config.json, CSDL, Backups) dang nam o %APPDATA%\MasterPlay. Neu chi doi
    /// hang so ten thu muc thi app se tao mot thu muc QuestLog rong va nguoi
    /// dung nhin thay ca thu vien BIEN MAT — nen o day co buoc di doi mot lan:
    ///
    ///   1. Chua co thu muc moi ma con thu muc cu  -> doi ten ca thu muc.
    ///   2. Trong thu muc do, MasterPlay.db        -> QuestLog.db.
    ///   3. config.json dang tro vao duong dan cu  -> viet lai cho tro sang moi.
    ///
    /// Doi ten that bai (file dang bi mo, thieu quyen) thi VAN DUNG THU MUC CU
    /// — tha giu ten cu con hon lam mat du lieu.
    /// </summary>
    public static class AppPaths
    {
        private const string FolderName = "QuestLog";
        private const string LegacyFolderName = "MasterPlay";
        private const string LocationFileName = "data-location.json";
        private const string LocationProperty = "dataDirectory";

        public const string ConfigFileName = "config.json";
        public const string DatabaseFileName = "QuestLog.db";
        public const string DatabaseTemplateFileName = "QuestLog.Template.db";
        private const string LegacyDatabaseFileName = "MasterPlay.db";

        private static readonly object _migrationLock = new object();
        private static string? _dataDirectory;

        /// <summary>Thu muc du lieu khi nguoi dung khong chon gi khac: %APPDATA%\QuestLog.</summary>
        public static string DefaultDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        private static string LegacyDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyFolderName);

        /// <summary>
        /// File ghi thu muc du lieu da chon luc cai. Nam o %LOCALAPPDATA% (theo may, khong
        /// roaming: o D:\ cua may nay chua chac co o may khac) va KHONG nam trong thu muc
        /// du lieu — phai doc no truoc thi moi biet thu muc du lieu o dau.
        /// </summary>
        public static string LocationFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName,
            LocationFileName);

        /// <summary>Thu muc du lieu cua app, da di doi xong neu can.</summary>
        public static string GetDataDirectory()
        {
            if (_dataDirectory != null) return _dataDirectory;

            lock (_migrationLock)
            {
                if (_dataDirectory != null) return _dataDirectory;

                string current = DefaultDataDirectory;
                string legacy = LegacyDataDirectory;

                // Chon dung thu muc mac dinh thi van di qua buoc di doi ben duoi: may con
                // du lieu MasterPlay cu ma bo qua buoc nay la thu vien bien mat.
                string? chosen = ReadChosenDataDirectory(LocationFilePath);
                if (chosen != null && !IsSamePath(chosen, current))
                {
                    // Khong tao duoc (o dia da rut ra, mat quyen) thi de loi noi len, KHONG
                    // lui ve thu muc mac dinh: mo mot thu vien rong trong khi du lieu that
                    // van con chinh la cai bay ma lop nay dung ra de tranh.
                    Directory.CreateDirectory(chosen);
                    // Nguoi dung co the chon thang thu muc MasterPlay cu luc cai.
                    MigrateDatabaseFileName(chosen);
                    _dataDirectory = chosen;
                    return _dataDirectory;
                }

                if (!Directory.Exists(current) && Directory.Exists(legacy))
                {
                    try
                    {
                        Directory.Move(legacy, current);
                    }
                    catch (Exception)
                    {
                        // Khong doi duoc thi bam vao thu muc cu, du lieu van nguyen ven.
                        _dataDirectory = legacy;
                        return _dataDirectory;
                    }
                }

                Directory.CreateDirectory(current);
                MigrateDatabaseFileName(current);
                _dataDirectory = current;
                return _dataDirectory;
            }
        }

        public static string GetDatabasePath() => Path.Combine(GetDataDirectory(), DatabaseFileName);

        /// <summary>
        /// Thu muc du lieu da chon luc cai, hoac null neu chua chon (dung mac dinh).
        /// File co ma hong thi NEM <see cref="InvalidDataException"/> thay vi lang le
        /// dung thu muc mac dinh — Shell bat loi nay va mo lai man chon thu muc.
        /// </summary>
        public static string? ReadChosenDataDirectory(string locationFile)
        {
            if (!File.Exists(locationFile)) return null;

            string? directory;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(locationFile));
                directory = document.RootElement.ValueKind == JsonValueKind.Object &&
                            document.RootElement.TryGetProperty(LocationProperty, out var value) &&
                            value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{locationFile} is not valid JSON.", ex);
            }

            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            {
                throw new InvalidDataException($"{locationFile} does not contain a full {LocationProperty} path.");
            }

            return Path.GetFullPath(directory);
        }

        public static void SaveChosenDataDirectory(string locationFile, string directory)
        {
            if (!Path.IsPathFullyQualified(directory))
            {
                throw new ArgumentException("The data directory must be a full path.", nameof(directory));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(locationFile)!);
            var json = JsonSerializer.Serialize(
                new Dictionary<string, string> { [LocationProperty] = Path.GetFullPath(directory) },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // Giu nguyen chu co dau trong duong dan cho de doc.
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                });

            // Ghi ra file tam roi moi thay: mat dien giua chung khong de lai file hong.
            string temporary = locationFile + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, locationFile, overwrite: true);
        }

        /// <summary>Thu muc nay da co du lieu QuestLog (config hoac CSDL, ke ca ten cu) chua.</summary>
        public static bool ContainsData(string directory) =>
            File.Exists(Path.Combine(directory, ConfigFileName)) ||
            File.Exists(Path.Combine(directory, DatabaseFileName)) ||
            File.Exists(Path.Combine(directory, LegacyDatabaseFileName));

        /// <summary>May nay da tung dung QuestLog/MasterPlay voi thu muc mac dinh.</summary>
        public static bool HasDefaultData() =>
            ContainsData(DefaultDataDirectory) || ContainsData(LegacyDataDirectory);

        public static bool IsSamePath(string left, string right) =>
            string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Duong dan cu con tro vao thu muc MasterPlay thi keo sang thu muc moi.
        /// Dung cho cac duong dan da luu trong config.json.
        /// </summary>
        public static string? Rebase(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;

            string legacy = LegacyDataDirectory;
            if (!storedPath.StartsWith(legacy, StringComparison.OrdinalIgnoreCase)) return storedPath;

            string rest = storedPath.Substring(legacy.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rebased = Path.Combine(GetDataDirectory(), rest);

            // Ten file CSDL cung doi theo.
            if (Path.GetFileName(rebased).Equals(LegacyDatabaseFileName, StringComparison.OrdinalIgnoreCase))
            {
                rebased = Path.Combine(Path.GetDirectoryName(rebased)!, DatabaseFileName);
            }
            return rebased;
        }

        private static void MigrateDatabaseFileName(string directory)
        {
            try
            {
                string current = Path.Combine(directory, DatabaseFileName);
                string legacy = Path.Combine(directory, LegacyDatabaseFileName);
                if (!File.Exists(current) && File.Exists(legacy))
                {
                    File.Move(legacy, current);
                }
            }
            catch (Exception)
            {
                // Giu nguyen file cu; DataService se tu xu ly truong hop thieu file.
            }
        }
    }
}
