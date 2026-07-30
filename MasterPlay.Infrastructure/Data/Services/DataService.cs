using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Data.Services
{
    public static class DataService
    {
        static DataService()
        {
            try
            {

                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MasterPlay");
                Directory.CreateDirectory(appDataDir);

                string dbPath = Path.Combine(appDataDir, "MasterPlay.db");
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MasterPlay.Template.db");

                if (!File.Exists(dbPath) && File.Exists(templatePath))
                {
                    File.Copy(templatePath, dbPath);
                }


                using (var context = new MasterPlayContext())
                {
                    context.Database.EnsureCreated();
                    context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                    context.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
                    context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=10000;");
                    context.Database.ExecuteSqlRaw(
                        """
                        CREATE TABLE IF NOT EXISTS GameOwnershipInfo (
                            GameId TEXT NOT NULL CONSTRAINT PK_GameOwnershipInfo PRIMARY KEY,
                            SteamAccount TEXT NULL,
                            SteamPassword TEXT NULL,
                            UbisoftAccount TEXT NULL,
                            UbisoftPassword TEXT NULL,
                            SupportDocumentUrl TEXT NULL,
                            DownloadUrl TEXT NULL,
                            Note TEXT NULL,
                            CONSTRAINT FK_GameOwnershipInfo_Games_GameId
                                FOREIGN KEY (GameId) REFERENCES Games (Id) ON DELETE CASCADE
                        );
                        """);
                    context.Database.ExecuteSqlRaw(
                        """
                        INSERT OR IGNORE INTO OwnershipStatus (Id, Name, SortOrder, ColorCode)
                        VALUES (6, 'Steam Off', 2, '#66C0F4');
                        UPDATE OwnershipStatus
                        SET SortOrder = CASE Id
                            WHEN 1 THEN 1
                            WHEN 6 THEN 2
                            WHEN 2 THEN 3
                            WHEN 3 THEN 4
                            WHEN 4 THEN 5
                            WHEN 5 THEN 6
                            ELSE SortOrder
                        END
                        WHERE Id IN (1, 2, 3, 4, 5, 6);
                        UPDATE ProgressStatus
                        SET Name = '⛔ Drop'
                        WHERE Id = 5;
                        UPDATE ProgressStatus
                        SET ColorCode = '#F5A623'
                        WHERE Id = 4 OR lower(trim(Name)) IN ('planned', 'intended');
                        """);
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dataservice_error.log");
                File.AppendAllText(logPath, $"{DateTime.Now}: {ex}\n");
                throw;
            }
        }

        #region Games
        public static List<Game> LoadGames()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Games.GetAllWithDetails().ToList();
            }
        }

        public static List<Game> LoadActiveGames()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Games.GetActiveGames().ToList();
            }
        }

        public static Game GetGameById(string id)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Games.GetByIdWithDetails(id);
            }
        }
        public static void AddGame(Game game)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var existing = uow.Games.GetById(game.Id);
                if (existing != null)
                    throw new InvalidOperationException("Game with this ID already exists.");
                uow.Games.Add(game);
                uow.Complete();
            }
        }

        public static void UpdateGame(Game game)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                uow.Games.UpdateWithDetails(game);
                uow.Complete();
            }
        }

        public static async Task RecordPlaytimeAsync(
            IReadOnlyDictionary<string, int> elapsedSeconds,
            CancellationToken cancellationToken = default)
        {
            if (elapsedSeconds.Count == 0)
                return;

            await using var context = new MasterPlayContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var today = DateTime.Today;

            foreach (var entry in elapsedSeconds.Where(x => x.Value > 0))
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Games SET TotalPlayTimeSeconds = COALESCE(TotalPlayTimeSeconds, 0) + {entry.Value} WHERE Id = {entry.Key}",
                    cancellationToken);

                var diary = await context.PlayDiaries
                    .FirstOrDefaultAsync(
                        x => x.GameId == entry.Key && x.Date.Date == today,
                        cancellationToken);

                if (diary == null)
                {
                    context.PlayDiaries.Add(new PlayDiary
                    {
                        GameId = entry.Key,
                        Date = today,
                        TimeSpent = entry.Value
                    });
                }
                else
                {
                    diary.TimeSpent += entry.Value;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public static void UpdateGamePositions(IEnumerable<Game> games)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                foreach (var game in games)
                {
                    uow.Games.UpdateWithDetails(game);
                }
                uow.Complete();
            }
        }
        #endregion

        #region Detected Processes
        public static List<DetectedProcess> LoadDetectedProcesses()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.DetectedProcesses.GetAll().ToList();
            }
        }

        public static void UpdateQueueOrderAndProgress1(List<Game> orderedGames)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {

                var allPositions = uow.GamePositions.GetAll().ToList();
                if (allPositions.Any())
                    uow.GamePositions.RemoveRange(allPositions);


                var positions = new List<GamePosition>();
                for (int i = 0; i < orderedGames.Count; i++)
                {
                    positions.Add(new GamePosition
                    {
                        GameId = orderedGames[i].Id,
                        PlayOrder = i + 1
                    });
                }
                uow.GamePositions.AddRange(positions);


                foreach (var game in orderedGames)
                {
                    uow.Games.UpdateWithDetails(game);
                }

                uow.Complete();
            }
        }


        public static void UpdateQueueOrderAndProgress(List<Game> orderedGames)
        {
            using (var context = new MasterPlayContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {

                        context.Database.ExecuteSqlRaw("DELETE FROM GamePosition");


                        for (int i = 0; i < orderedGames.Count; i++)
                        {
                            context.Database.ExecuteSqlRaw(
                                "INSERT INTO GamePosition (GameId, PlayOrder) VALUES ({0}, {1})",
                                orderedGames[i].Id, i + 1);
                        }


                        foreach (var game in orderedGames)
                        {
                            context.Database.ExecuteSqlRaw(
                                "UPDATE Games SET ProgressId = {0} WHERE Id = {1}",
                                game.ProgressId, game.Id);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"UpdateQueueOrderAndProgress failed: {ex.Message}", ex);
                    }
                }
            }
        }

        public static void SyncDetectedProcesses(List<DetectedProcess> processes)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var existing = uow.DetectedProcesses.GetAll().ToList();











                var existingPaths = new HashSet<string>(
                    existing.Select(p => p.FullPath), StringComparer.OrdinalIgnoreCase);
                var newPaths = new HashSet<string>(
                    processes.Select(p => p.FullPath), StringComparer.OrdinalIgnoreCase);

                var toRemove = existing.Where(p => !newPaths.Contains(p.FullPath)).ToList();
                var toAdd = processes.Where(p => !existingPaths.Contains(p.FullPath)).ToList();

                if (toRemove.Count > 0)
                    uow.DetectedProcesses.RemoveRange(toRemove);
                if (toAdd.Count > 0)
                    uow.DetectedProcesses.AddRange(toAdd);

                uow.Complete();
            }
        }
        #endregion

        #region Selections
        public static List<Genre> LoadGenresList()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Genres.GetAll().ToList();
            }
        }

        public static List<Theme> LoadThemesList()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Themes.GetAll().ToList();
            }
        }

        public static List<Series> LoadSeriesList()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.Series.GetAll().ToList();
            }
        }

        public static List<ProgressStatus> LoadProgressList()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.ProgressStatuses.GetAll()
                    .OrderBy(p => p.SortOrder)
                    .ToList();
            }
        }

        public static List<OwnershipStatus> LoadOwnershipList()
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.OwnershipStatuses.GetAll()
                    .OrderBy(o => o.SortOrder)
                    .ToList();
            }
        }

        public static List<Genre> GetGameGenres(string gameId)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.GameGenres
                    .Find(gg => gg.GameId == gameId)
                    .Select(gg => gg.Genre)
                    .ToList();
            }
        }

        public static List<Theme> GetGameThemes(string gameId)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                return uow.GameThemes
                    .Find(gt => gt.GameId == gameId)
                    .Select(gt => gt.Theme)
                    .ToList();
            }
        }

        public static Series AddSeries(string seriesName)
        {
            if (string.IsNullOrWhiteSpace(seriesName))
                throw new ArgumentException("Series name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var existing = uow.Series.GetAll()
                    .FirstOrDefault(s => s.Name.Equals(seriesName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;

                var series = new Series { Name = seriesName };
                uow.Series.Add(series);
                uow.Complete();
                return series;
            }
        }

        public static Genre AddGenre(string genreName)
        {
            if (string.IsNullOrWhiteSpace(genreName))
                throw new ArgumentException("Genre name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var existing = uow.Genres.GetAll()
                    .FirstOrDefault(g => g.Name.Equals(genreName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;

                var genre = new Genre { Name = genreName };
                uow.Genres.Add(genre);
                uow.Complete();
                return genre;
            }
        }

        public static Theme AddTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                throw new ArgumentException("Theme name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var existing = uow.Themes.GetAll()
                    .FirstOrDefault(t => t.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;

                var theme = new Theme { Name = themeName };
                uow.Themes.Add(theme);
                uow.Complete();
                return theme;
            }
        }


        public static bool UpdateSeries(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Series name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var series = uow.Series.GetById(id);
                if (series == null) return false;


                var existing = uow.Series.GetAll()
                    .FirstOrDefault(s => s.Id != id && s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    throw new InvalidOperationException("Another series with this name already exists.");

                series.Name = newName;
                uow.Series.Update(series);
                uow.Complete();
                return true;
            }
        }

        public static bool UpdateGenre(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Genre name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var genre = uow.Genres.GetById(id);
                if (genre == null) return false;

                var existing = uow.Genres.GetAll()
                    .FirstOrDefault(g => g.Id != id && g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    throw new InvalidOperationException("Another genre with this name already exists.");

                genre.Name = newName;
                uow.Genres.Update(genre);
                uow.Complete();
                return true;
            }
        }

        public static bool UpdateTheme(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Theme name cannot be empty");

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var theme = uow.Themes.GetById(id);
                if (theme == null) return false;

                var existing = uow.Themes.GetAll()
                    .FirstOrDefault(t => t.Id != id && t.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    throw new InvalidOperationException("Another theme with this name already exists.");

                theme.Name = newName;
                uow.Themes.Update(theme);
                uow.Complete();
                return true;
            }
        }


        public static bool DeleteSeries(int id)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var series = uow.Series.GetById(id);
                if (series == null) return false;



                uow.Series.Remove(series);
                uow.Complete();
                return true;
            }
        }

        public static bool DeleteGenre(int id)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var genre = uow.Genres.GetById(id);
                if (genre == null) return false;


                var gameGenres = uow.GameGenres.Find(gg => gg.GenreId == id);
                uow.GameGenres.RemoveRange(gameGenres);

                uow.Genres.Remove(genre);
                uow.Complete();
                return true;
            }
        }

        public static bool DeleteTheme(int id)
        {
            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {
                var theme = uow.Themes.GetById(id);
                if (theme == null) return false;


                var gameThemes = uow.GameThemes.Find(gt => gt.ThemeId == id);
                uow.GameThemes.RemoveRange(gameThemes);

                uow.Themes.Remove(theme);
                uow.Complete();
                return true;
            }
        }
        #endregion

        #region System Logic
        public static void SyncGameStatus()
        {
            var games = LoadGames();
            var availables = LoadDetectedProcesses();

            foreach (var game in games)
            {
                try
                {
                    string processName = game.ExeName?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
                        ? game.ExeName : game.ExeName + ".exe";

                    var match = availables.FirstOrDefault(a =>
                        string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase));

                    bool newReady = match != null && FileHelper.FileCheck(match.FullPath);


                    if (newReady)
                    {
                        CreateBackupFolder(game);
                    }

                    if (game.Ready != newReady)
                    {
                        game.Ready = newReady;
                        UpdateGame(game);
                    }
                }
                catch
                {
                    if (game.Ready)
                    {
                        game.Ready = false;
                        UpdateGame(game);
                    }
                }
            }
        }

        public static bool CreateBackupFolder(Game game)
        {
            if (game == null) return false;

            if (!string.IsNullOrWhiteSpace(game.BackupFolderPath) && Directory.Exists(game.BackupFolderPath))
                return true;

            string backupRoot = JsonService.GetBackupRootPath();
            if (string.IsNullOrWhiteSpace(backupRoot))
                return false;

            string validName = ValidFolderName(game.Name);
            string uniquePath = GetUniqueFolderPath(backupRoot, validName);

            try
            {
                Directory.CreateDirectory(uniquePath);
                game.BackupFolderPath = uniquePath;

                if (!string.IsNullOrWhiteSpace(game.SaveFolderPath))
                {

                    string txtPath = Path.Combine(uniquePath, "SaveLocation.txt");
                    File.WriteAllText(txtPath, game.SaveFolderPath);
                }

                UpdateGame(game);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RenameBackupFolder(Game game, string newName)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.BackupFolderPath))
                return false;

            string oldPath = game.BackupFolderPath;
            if (!Directory.Exists(oldPath))
                return false;

            string backupRoot = Path.GetDirectoryName(oldPath);
            string validNewName = ValidFolderName(newName);
            string desiredPath = Path.Combine(backupRoot, validNewName);

            if (string.Equals(oldPath, desiredPath, StringComparison.OrdinalIgnoreCase))
                return true;

            string newPath = desiredPath;
            if (Directory.Exists(desiredPath))
            {
                newPath = GetUniqueFolderPath(backupRoot, validNewName);
            }

            try
            {
                Directory.Move(oldPath, newPath);
                game.BackupFolderPath = newPath;
                UpdateGame(game);
                return true;
            }
            catch
            {
                return false;
            }
        }


        public static void HandleProgressChange(Game game, int? oldProgressId, int? newProgressId)
        {
            if (game == null) return;
            if (oldProgressId == newProgressId) return;

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {

                var allQueueGames = uow.Games.GetAllWithDetails()
                    .Where(g => !g.IsDisabled && (g.ProgressId == 2 || g.ProgressId == 3 || g.ProgressId == 4))
                    .ToList();

                var withPositions = allQueueGames
                    .Where(g => g.GamePosition != null && g.GamePosition.PlayOrder.HasValue)
                    .OrderBy(g => g.GamePosition.PlayOrder)
                    .ToList();


                if (newProgressId == 3 && oldProgressId != 3)
                {

                    var nextPlayGames = withPositions.Where(g => g.ProgressId == 3).ToList();
                    if (nextPlayGames.Any())
                    {

                        var lastNextPlay = nextPlayGames.OrderByDescending(g => g.GamePosition.PlayOrder).First();
                        int targetOrder = lastNextPlay.GamePosition.PlayOrder.Value;


                        if (game.GamePosition == null)
                        {
                            game.GamePosition = new GamePosition { GameId = game.Id };
                            uow.GamePositions.Add(game.GamePosition);
                        }
                        game.GamePosition.PlayOrder = targetOrder;


                        lastNextPlay.ProgressId = 4;
                        lastNextPlay.Progress = uow.ProgressStatuses.GetById(4);
                        if (lastNextPlay.GamePosition != null)
                        {
                            uow.GamePositions.Remove(lastNextPlay.GamePosition);
                            lastNextPlay.GamePosition = null;
                        }

                        uow.Games.UpdateWithDetails(game);
                        uow.Games.UpdateWithDetails(lastNextPlay);
                    }
                    else
                    {

                        int nextOrder = withPositions.Any() ? withPositions.Max(g => g.GamePosition.PlayOrder.Value) + 1 : 1;
                        if (game.GamePosition == null)
                        {
                            game.GamePosition = new GamePosition { GameId = game.Id };
                            uow.GamePositions.Add(game.GamePosition);
                        }
                        game.GamePosition.PlayOrder = nextOrder;
                        uow.Games.UpdateWithDetails(game);
                    }
                }

                else if (oldProgressId == 3 && newProgressId != 3)
                {

                    if (game.GamePosition != null)
                    {
                        uow.GamePositions.Remove(game.GamePosition);
                        game.GamePosition = null;
                    }


                    var intendedGames = withPositions.Where(g => g.ProgressId == 4).ToList();
                    if (intendedGames.Any())
                    {
                        var firstIntended = intendedGames.OrderBy(g => g.GamePosition.PlayOrder).First();
                        int targetOrder = firstIntended.GamePosition.PlayOrder.Value;


                        firstIntended.ProgressId = 3;
                        firstIntended.Progress = uow.ProgressStatuses.GetById(3);


                        uow.Games.UpdateWithDetails(firstIntended);
                    }


                    uow.Games.UpdateWithDetails(game);
                }

                uow.Complete();
            }
        }
        public static void ReorderOnPromoteToNextPlay(Game promotedGame)
        {
            if (promotedGame == null || promotedGame.ProgressId != 3)
                return;

            using (var uow = new UnitOfWork(new MasterPlayContext()))
            {

                var gameToPromote = uow.Games.GetByIdWithDetails(promotedGame.Id);
                if (gameToPromote == null) return;


                var allQueueGames = uow.Games.GetAllWithDetails()
                    .Where(g => !g.IsDisabled && (g.ProgressId == 2 || g.ProgressId == 3 || g.ProgressId == 4))
                    .ToList();


                var withPositions = allQueueGames
                    .Where(g => g.GamePosition != null && g.GamePosition.PlayOrder.HasValue)
                    .OrderBy(g => g.GamePosition.PlayOrder)
                    .ToList();


                var nextPlayGames = withPositions.Where(g => g.ProgressId == 3).ToList();

                if (nextPlayGames.Any())
                {

                    var lastNextPlay = nextPlayGames.OrderByDescending(g => g.GamePosition.PlayOrder).First();
                    int targetOrder = lastNextPlay.GamePosition.PlayOrder.Value;


                    if (gameToPromote.GamePosition == null)
                    {

                        gameToPromote.GamePosition = new GamePosition { GameId = gameToPromote.Id };
                        uow.GamePositions.Add(gameToPromote.GamePosition);
                    }

                    gameToPromote.GamePosition.PlayOrder = targetOrder;


                    lastNextPlay.ProgressId = 4;
                    lastNextPlay.Progress = uow.ProgressStatuses.GetById(4);

                    if (lastNextPlay.GamePosition != null)
                    {
                        uow.GamePositions.Remove(lastNextPlay.GamePosition);
                        lastNextPlay.GamePosition = null;
                    }


                    uow.Games.UpdateWithDetails(gameToPromote);
                    uow.Games.UpdateWithDetails(lastNextPlay);
                }
                else
                {

                    int nextOrder = withPositions.Any() ? withPositions.Max(g => g.GamePosition.PlayOrder.Value) + 1 : 1;

                    if (gameToPromote.GamePosition == null)
                    {
                        gameToPromote.GamePosition = new GamePosition { GameId = gameToPromote.Id };
                        uow.GamePositions.Add(gameToPromote.GamePosition);
                    }
                    gameToPromote.GamePosition.PlayOrder = nextOrder;
                    uow.Games.UpdateWithDetails(gameToPromote);
                }

                uow.Complete();
            }
        }
        public static bool UpdateSaveLocationFile(Game game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.BackupFolderPath) || !Directory.Exists(game.BackupFolderPath))
                return false;

            if (string.IsNullOrWhiteSpace(game.SaveFolderPath))
                return false;

            try
            {
                string txtPath = Path.Combine(game.BackupFolderPath, "SaveLocation.txt");
                File.WriteAllText(txtPath, game.SaveFolderPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetUniqueFolderPath(string root, string desiredName)
        {
            string basePath = Path.Combine(root, desiredName);
            string finalPath = basePath;
            int counter = 1;
            while (Directory.Exists(finalPath))
            {
                finalPath = Path.Combine(root, $"{desiredName} ({counter})");
                counter++;
            }
            return finalPath;
        }
        public static string ValidFolderName(string gameName)
        {
            char[] invalid = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            string result = new string(gameName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
            result = Regex.Replace(result, "-+", "-").Trim('-', ' ');
            if (result.Length > 200) result = result.Substring(0, 200).TrimEnd('-');
            return result;
        }
        #endregion

    }
}
