using MasterPlay.Models;
using MasterPlay.Models.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels.Generral
{
    public static class ViewModelHelpers
    {
        // --- Input Formatting ---
        public static void InputFormatter(Game game)
        {
            if (game == null) return;

            // 1. Xử lý Tên Game: Nếu trống thì lấy từ Config cũ
            if (string.IsNullOrWhiteSpace(game.Name))
            {
                var original = DataHelper.LoadGames().FirstOrDefault(g => g.Id == game.Id);
                game.Name = original?.Name ?? "Unknown Game";
            }

            // 2. ExeName: Tự xóa đuôi .exe
            if (!string.IsNullOrWhiteSpace(game.ExeName))
            {
                string trimmed = game.ExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    game.ExeName = trimmed[..^4].Trim();
                }
            }

            game.SaveFolderPath = game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.BackupFolderPath = game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.ImagePath = game.ImagePath?.Trim('"').Trim() ?? string.Empty;
        }

        // --- Folder Name Validation ---
        public static string ValidFolderName(string gameName)
        {
            char[] invalid = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            string result = new string(gameName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
            result = Regex.Replace(result, "-+", "-").Trim('-', ' ');
            if (result.Length > 200) result = result.Substring(0, 200).TrimEnd('-');
            return result;
        }

        // --- Image Loading ---
        public static BitmapImage? LoadPreviewImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}