using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using MasterPlay.Core.Taxonomy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Infrastructure.Data.Schema
{
    /// <summary>
    /// Taxonomy theo chu so huu + nhom theme. Spec 2026-10-04-theme-groups-design §4.5.
    /// Idempotent: chay lai tren DB da migrate khong doi gi.
    ///
    /// CANH BAO — vi sao phai DUNG LAI bang Themes/Genres:
    /// DB cu khai bao `Name TEXT NOT NULL UNIQUE` NGAY TREN COT (sqlite_autoindex_*),
    /// thu ma DROP INDEX khong go duoc. Ma GameThemes/GameGenres tro vao hai bang nay
    /// bang `ON DELETE CASCADE`: DROP TABLE luc foreign_keys dang BAT se xoa SACH lien
    /// ket game↔tag ma khong bao loi. Vi vay: sao luu truoc, tat foreign_keys (ngoai
    /// transaction — trong transaction PRAGMA nay vo tac dung), dung lai trong mot
    /// transaction, foreign_key_check truoc khi commit.
    /// </summary>
    public static class ThemeGroupsMigration
    {
        private static readonly (string NameEn, string NameVi, string[] Themes)[] SeedGroups =
        {
            ("Era", "Thời đại", new[]
            {
                "Ancient", "Prehistoric", "Medieval", "Renaissance", "Early modern", "Steam Punk",
                "Modern", "Retro", "Near Future", "Far Future", "Cyberpunk", "Post-Apocalyptic",
            }),
            ("Setting", "Bối cảnh", new[]
            {
                "Chinese", "Feudal Japan", "Greece", "Roman", "Egypt", "Middle East", "Nordic",
                "Far West", "Jungle", "Ocean", "Glacial", "Desert", "Urban", "Villages",
                "Underground", "Space", "Micro World",
            }),
            ("World Style", "Phong cách thế giới", new[]
            {
                "Historical", "Sci-fi", "Fantasy", "Mythology", "Dark", "Gothic", "Horror",
            }),
            ("Character", "Nhân vật", new[]
            {
                "Military", "Agent / Spy", "Pirate", "Spartan", "Super Hero", "Super Power",
            }),
        };

        public static void Apply(MasterPlayContext context)
        {
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            context.Database.OpenConnection();
            try
            {
                Execute(connection, null,
                    """
                    CREATE TABLE IF NOT EXISTS ThemeGroups (
                        Id INTEGER NOT NULL CONSTRAINT PK_ThemeGroups PRIMARY KEY AUTOINCREMENT,
                        LibraryOwnerKey TEXT NOT NULL DEFAULT 'system:shared',
                        NameEn TEXT NOT NULL,
                        NameVi TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS IX_ThemeGroups_LibraryOwnerKey_SortOrder
                        ON ThemeGroups (LibraryOwnerKey, SortOrder);
                    """);

                var rebuildThemes = !ColumnExists(connection, "Themes", "NameKey");
                var rebuildGenres = !ColumnExists(connection, "Genres", "NameKey");
                if (rebuildThemes || rebuildGenres)
                {
                    Backup(connection);
                    RebuildTables(connection, rebuildThemes, rebuildGenres);
                }

                FillNameKeys(connection, "Themes");
                FillNameKeys(connection, "Genres");
                Execute(connection, null,
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Themes_LibraryOwnerKey_NameKey ON Themes (LibraryOwnerKey, NameKey);
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Genres_LibraryOwnerKey_NameKey ON Genres (LibraryOwnerKey, NameKey);
                    CREATE INDEX IF NOT EXISTS IX_Themes_GroupId ON Themes (GroupId);
                    """);

                SeedSharedGroups(connection);
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }

        private static void Backup(SqliteConnection connection)
        {
            var source = connection.DataSource;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return;

            var directory = Path.GetDirectoryName(source)!;
            var name = Path.GetFileNameWithoutExtension(source);
            var target = Path.Combine(directory, $"{name}.before-theme-groups-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            using var destination = new SqliteConnection($"Data Source={target};Pooling=False");
            destination.Open();
            connection.BackupDatabase(destination);
        }

        private static void RebuildTables(SqliteConnection connection, bool themes, bool genres)
        {
            Execute(connection, null, "PRAGMA foreign_keys=OFF;");
            try
            {
                using var transaction = connection.BeginTransaction();
                if (themes)
                {
                    Execute(connection, transaction,
                        """
                        CREATE TABLE Themes_new (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            IconData TEXT NULL,
                            SortOrder INTEGER NOT NULL DEFAULT 0,
                            LibraryOwnerKey TEXT NOT NULL DEFAULT 'system:shared',
                            NameKey TEXT NOT NULL DEFAULT '',
                            GroupId INTEGER NULL REFERENCES ThemeGroups (Id) ON DELETE SET NULL
                        );
                        INSERT INTO Themes_new (Id, Name, IconData, SortOrder)
                            SELECT Id, Name, IconData, SortOrder FROM Themes;
                        DROP TABLE Themes;
                        ALTER TABLE Themes_new RENAME TO Themes;
                        """);
                }

                if (genres)
                {
                    Execute(connection, transaction,
                        """
                        CREATE TABLE Genres_new (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            IconData TEXT NULL,
                            SortOrder INTEGER NOT NULL DEFAULT 0,
                            LibraryOwnerKey TEXT NOT NULL DEFAULT 'system:shared',
                            NameKey TEXT NOT NULL DEFAULT ''
                        );
                        INSERT INTO Genres_new (Id, Name, IconData, SortOrder)
                            SELECT Id, Name, IconData, SortOrder FROM Genres;
                        DROP TABLE Genres;
                        ALTER TABLE Genres_new RENAME TO Genres;
                        """);
                }

                using (var check = connection.CreateCommand())
                {
                    check.Transaction = transaction;
                    check.CommandText = "PRAGMA foreign_key_check;";
                    using var reader = check.ExecuteReader();
                    if (reader.Read())
                        throw new InvalidOperationException(
                            $"Theme groups migration: foreign_key_check failed on table {reader.GetString(0)}; rolled back.");
                }

                transaction.Commit();
            }
            finally
            {
                Execute(connection, null, "PRAGMA foreign_keys=ON;");
            }
        }

        /// <summary>
        /// Tinh NameKey cho hang con rong. Neu hai ten cu chi khac hoa/thuong (truoc day
        /// UNIQUE(Name) phan biet hoa thuong nen co the ton tai), hang sau nhan khoa co
        /// hau to "#id" de unique index tao duoc — CHI khoa doi, ten hien thi giu nguyen.
        /// </summary>
        private static void FillNameKeys(SqliteConnection connection, string table)
        {
            var rows = new List<(long Id, string Owner, string Name)>();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            using (var read = connection.CreateCommand())
            {
                read.CommandText = $"SELECT Id, LibraryOwnerKey, Name, NameKey FROM [{table}] ORDER BY Id;";
                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    if (key.Length > 0) taken.Add(reader.GetString(1) + "\n" + key);
                    else rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
                }
            }

            if (rows.Count == 0) return;

            using var transaction = connection.BeginTransaction();
            foreach (var row in rows)
            {
                var key = TaxonomyRules.NormalizeName(row.Name);
                if (key.Length == 0 || !taken.Add(row.Owner + "\n" + key))
                {
                    key = $"{key}#{row.Id}";
                    taken.Add(row.Owner + "\n" + key);
                }

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = $"UPDATE [{table}] SET NameKey = $key WHERE Id = $id;";
                update.Parameters.AddWithValue("$key", key);
                update.Parameters.AddWithValue("$id", row.Id);
                update.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        /// <summary>Chi chay khi ThemeGroups rong — tuc lan dau. Khong dung lai nhom admin da xoa.</summary>
        private static void SeedSharedGroups(SqliteConnection connection)
        {
            using (var count = connection.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM ThemeGroups;";
                if (Convert.ToInt64(count.ExecuteScalar()) > 0) return;
            }

            using var transaction = connection.BeginTransaction();
            for (var index = 0; index < SeedGroups.Length; index++)
            {
                var (nameEn, nameVi, themes) = SeedGroups[index];
                long groupId;
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText =
                        "INSERT INTO ThemeGroups (LibraryOwnerKey, NameEn, NameVi, SortOrder) " +
                        "VALUES ($owner, $en, $vi, $sort); SELECT last_insert_rowid();";
                    insert.Parameters.AddWithValue("$owner", TaxonomyRules.SharedOwnerKey);
                    insert.Parameters.AddWithValue("$en", nameEn);
                    insert.Parameters.AddWithValue("$vi", nameVi);
                    insert.Parameters.AddWithValue("$sort", index);
                    groupId = Convert.ToInt64(insert.ExecuteScalar());
                }

                foreach (var theme in themes)
                {
                    using var assign = connection.CreateCommand();
                    assign.Transaction = transaction;
                    assign.CommandText =
                        "UPDATE Themes SET GroupId = $group " +
                        "WHERE LibraryOwnerKey = $owner AND NameKey = $key AND GroupId IS NULL;";
                    assign.Parameters.AddWithValue("$group", groupId);
                    assign.Parameters.AddWithValue("$owner", TaxonomyRules.SharedOwnerKey);
                    assign.Parameters.AddWithValue("$key", TaxonomyRules.NormalizeName(theme));
                    assign.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }

        private static bool ColumnExists(SqliteConnection connection, string table, string column)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{table}]);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static void Execute(SqliteConnection connection, DbTransaction? transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction?)transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
