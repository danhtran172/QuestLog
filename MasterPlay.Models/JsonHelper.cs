using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace MasterPlay.Models
{
    /// <summary>
    /// Helper chuyên biệt để load/save danh sách Game từ file gameData.json.
    /// (Config.json giờ do ConfigManager xử lý riêng)
    /// </summary>
    public static class JsonHelper
    {
        private static readonly string GameDataPath = Path.Combine(
            Path.Combine(@"D:\\Apps\\[AppData]", "MasterPlay"),
            "gameData.json");

        /// <summary>
        /// Load danh sách game từ gameData.json. Tạo file rỗng nếu chưa tồn tại.
        /// </summary>
        public static List<Game> LoadGames()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameDataPath) ?? "");

            if (!File.Exists(GameDataPath))
            {
                File.WriteAllText(GameDataPath, "[]");
                return new List<Game>();
            }

            string json = File.ReadAllText(GameDataPath);
            var games = JsonConvert.DeserializeObject<List<Game>>(json);

            return games ?? new List<Game>();
        }

        /// <summary>
        /// Lưu danh sách game vào gameData.json
        /// </summary>
        public static void SaveGames(List<Game> games)
        {
            string json = JsonConvert.SerializeObject(games, Formatting.Indented);
            File.WriteAllText(GameDataPath, json);
        }

        // Nếu cần thêm hàm khác cho game data (ví dụ GetGameById, AddGame, RemoveGame...)
        // Có thể thêm ở đây để tiện dùng ở UI layer
        public static Game? GetGameById(Guid id)
        {
            var games = LoadGames();
            return games.Find(g => g.Id == id);
        }

        public static void AddGame(Game newGame)
        {
            var games = LoadGames();
            games.Add(newGame);
            SaveGames(games);
        }

        public static bool RemoveGame(Guid id)
        {
            var games = LoadGames();
            var removed = games.RemoveAll(g => g.Id == id) > 0;
            if (removed)
            {
                SaveGames(games);
            }
            return removed;
        }
    }
}