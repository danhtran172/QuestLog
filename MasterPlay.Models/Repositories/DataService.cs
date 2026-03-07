using MasterPlay.Helpers;
using MasterPlay.Models.Models;
using MasterPlay.Models.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MasterPlay.Models
{
    public static class DataService
    {
        static DataService()
        {
            try
            {
                using (var context = new MasterPlayContext())
                {
                    context.Database.EnsureCreated();
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
                // Xóa tất cả GamePositions hiện có
                var allPositions = uow.GamePositions.GetAll().ToList();
                if (allPositions.Any())
                    uow.GamePositions.RemoveRange(allPositions);

                // Tạo GamePositions mới theo thứ tự
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

                // Cập nhật từng game (Progress đã được set ở ViewModel)
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
                        // Xóa toàn bộ GamePosition
                        context.Database.ExecuteSqlRaw("DELETE FROM GamePosition");

                        // Thêm mới từng game
                        for (int i = 0; i < orderedGames.Count; i++)
                        {
                            context.Database.ExecuteSqlRaw(
                                "INSERT INTO GamePosition (GameId, PlayOrder) VALUES ({0}, {1})",
                                orderedGames[i].Id, i + 1);
                        }

                        // Cập nhật ProgressId
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
                uow.DetectedProcesses.RemoveRange(existing);
                uow.DetectedProcesses.AddRange(processes);
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
                    // Đổi tên file cho đồng bộ với UpdateSaveLocationFile
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
                // Lấy tất cả game trong PlayQueue (ProgressId 2,3,4) có GamePosition
                var allQueueGames = uow.Games.GetAllWithDetails()
                    .Where(g => !g.IsDisabled && (g.ProgressId == 2 || g.ProgressId == 3 || g.ProgressId == 4))
                    .ToList();

                var withPositions = allQueueGames
                    .Where(g => g.GamePosition != null && g.GamePosition.PlayOrder.HasValue)
                    .OrderBy(g => g.GamePosition.PlayOrder)
                    .ToList();

                // Trường hợp 1: Promote lên NextPlay (new = 3, old != 3)
                if (newProgressId == 3 && oldProgressId != 3)
                {
                    // Tìm các game NextPlay hiện tại
                    var nextPlayGames = withPositions.Where(g => g.ProgressId == 3).ToList();
                    if (nextPlayGames.Any())
                    {
                        // Lấy game NextPlay có PlayOrder lớn nhất (cuối hàng đợi)
                        var lastNextPlay = nextPlayGames.OrderByDescending(g => g.GamePosition.PlayOrder).First();
                        int targetOrder = lastNextPlay.GamePosition.PlayOrder.Value;

                        // Gán PlayOrder cho game được promote
                        if (game.GamePosition == null)
                        {
                            game.GamePosition = new GamePosition { GameId = game.Id };
                            uow.GamePositions.Add(game.GamePosition);
                        }
                        game.GamePosition.PlayOrder = targetOrder;

                        // Chuyển lastNextPlay thành Intended (ProgressId = 4) và xóa GamePosition của nó
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
                        // Không có NextPlay nào: thêm vào cuối danh sách
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
                // Trường hợp 2: Demote khỏi NextPlay (old = 3, new != 3)
                else if (oldProgressId == 3 && newProgressId != 3)
                {
                    // Xóa GamePosition của game hiện tại (nếu có)
                    if (game.GamePosition != null)
                    {
                        uow.GamePositions.Remove(game.GamePosition);
                        game.GamePosition = null;
                    }

                    // Tìm game Intended (ProgressId = 4) có PlayOrder nhỏ nhất (đầu hàng đợi)
                    var intendedGames = withPositions.Where(g => g.ProgressId == 4).ToList();
                    if (intendedGames.Any())
                    {
                        var firstIntended = intendedGames.OrderBy(g => g.GamePosition.PlayOrder).First();
                        int targetOrder = firstIntended.GamePosition.PlayOrder.Value;

                        // Chuyển firstIntended thành NextPlay (ProgressId = 3)
                        firstIntended.ProgressId = 3;
                        firstIntended.Progress = uow.ProgressStatuses.GetById(3);
                        // Giữ nguyên PlayOrder của nó

                        uow.Games.UpdateWithDetails(firstIntended);
                    }
                    // Nếu không có Intended nào, thì không cần làm gì thêm

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
                // Load lại promotedGame từ database để có tracking đúng (tránh xung đột)
                var gameToPromote = uow.Games.GetByIdWithDetails(promotedGame.Id);
                if (gameToPromote == null) return;

                // Lấy tất cả game trong PlayQueue (ProgressId 2,3,4) có GamePosition
                var allQueueGames = uow.Games.GetAllWithDetails()
                    .Where(g => !g.IsDisabled && (g.ProgressId == 2 || g.ProgressId == 3 || g.ProgressId == 4))
                    .ToList();

                // Lọc các game có GamePosition (PlayOrder)
                var withPositions = allQueueGames
                    .Where(g => g.GamePosition != null && g.GamePosition.PlayOrder.HasValue)
                    .OrderBy(g => g.GamePosition.PlayOrder)
                    .ToList();

                // Tìm các game NextPlay (ProgressId == 3) có PlayOrder
                var nextPlayGames = withPositions.Where(g => g.ProgressId == 3).ToList();

                if (nextPlayGames.Any())
                {
                    // Lấy game NextPlay có PlayOrder lớn nhất (cao nhất - ở cuối hàng đợi)
                    var lastNextPlay = nextPlayGames.OrderByDescending(g => g.GamePosition.PlayOrder).First();
                    int targetOrder = lastNextPlay.GamePosition.PlayOrder.Value;

                    // Cập nhật GamePosition cho game được promote
                    if (gameToPromote.GamePosition == null)
                    {
                        // Nếu chưa có, tạo mới
                        gameToPromote.GamePosition = new GamePosition { GameId = gameToPromote.Id };
                        uow.GamePositions.Add(gameToPromote.GamePosition);
                    }
                    // Nếu đã có, chỉ cập nhật PlayOrder (không tạo mới)
                    gameToPromote.GamePosition.PlayOrder = targetOrder;

                    // Chuyển lastNextPlay thành Intended (ProgressId = 4) và xóa GamePosition của nó
                    lastNextPlay.ProgressId = 4;
                    lastNextPlay.Progress = uow.ProgressStatuses.GetById(4);

                    if (lastNextPlay.GamePosition != null)
                    {
                        uow.GamePositions.Remove(lastNextPlay.GamePosition);
                        lastNextPlay.GamePosition = null;
                    }

                    // Cập nhật các thay đổi
                    uow.Games.UpdateWithDetails(gameToPromote);
                    uow.Games.UpdateWithDetails(lastNextPlay);
                }
                else
                {
                    // Không có NextPlay nào: thêm vào cuối danh sách
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