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
                var original = DataService.LoadGames().FirstOrDefault(g => g.Id == game.Id);
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

            // 3. Trim các đường dẫn
            game.SaveFolderPath = game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.BackupFolderPath = game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.ImagePath = game.ImagePath?.Trim('"').Trim() ?? string.Empty;

            // 4. Validate Score: chỉ 1-10 hoặc 999
            if (game.Score != 999)
            {
                if (game.Score < 1 || game.Score > 10)
                {
                    game.Score = 0; // Reset về 0 nếu không hợp lệ
                                    // Có thể throw exception hoặc log nếu cần
                                    // throw new ArgumentException("Score phải từ 1-10 hoặc 999.");
                }
            }

            // 5. Validate Excited: chỉ 1-10 hoặc 999
            if (game.Excited != 999)
            {
                if (game.Excited < 1 || game.Excited > 10)
                {
                    game.Excited = 0; // Reset về 0 nếu không hợp lệ
                                      // throw new ArgumentException("Excited phải từ 1-10 hoặc 999.");
                }
            }

            // 6. Validate FinishedIn: phải là năm hợp lệ (1900 đến năm hiện tại + 1)
            //if (game.FinishedIn != null)
            //{
            //    string year = game.FinishedIn.to;
            //    string currentYear = DateTime.Now.ToString("yyyy");
            //    if (year < 2003 || year > currentYear + 1)
            //    {
            //        game.FinishedIn = null; // Reset nếu không hợp lệ
            //                                // throw new ArgumentException("Finished In phải là năm hợp lệ (1900 đến hiện tại + 1).");
            //    }
            //}
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