using System;
using System.IO;

namespace MasterPlay.Infrastructure.Configuration
{
    /// <summary>
    /// Noi DUY NHAT quyet dinh app ghi du lieu o dau, va la noi chuyen du lieu
    /// cu sang ten moi.
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

        public const string DatabaseFileName = "QuestLog.db";
        public const string DatabaseTemplateFileName = "QuestLog.Template.db";
        private const string LegacyDatabaseFileName = "MasterPlay.db";

        private static readonly object _migrationLock = new object();
        private static string? _dataDirectory;

        /// <summary>Thu muc du lieu cua app, da di doi xong neu can.</summary>
        public static string GetDataDirectory()
        {
            if (_dataDirectory != null) return _dataDirectory;

            lock (_migrationLock)
            {
                if (_dataDirectory != null) return _dataDirectory;

                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string current = Path.Combine(roaming, FolderName);
                string legacy = Path.Combine(roaming, LegacyFolderName);

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
        /// Duong dan cu con tro vao thu muc MasterPlay thi keo sang thu muc moi.
        /// Dung cho cac duong dan da luu trong config.json.
        /// </summary>
        public static string? Rebase(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;

            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string legacy = Path.Combine(roaming, LegacyFolderName);
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
