using MasterPlay.Models;
using MasterPlay.Models.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels.Generral
{
    public static class ViewModelHelpers
    {
        public static Game InputFormatter(Game game)
        {
            if (game == null) return game;

            if (string.IsNullOrWhiteSpace(game.Name))
            {
                var original = DataService.LoadGames().FirstOrDefault(g => g.Id == game.Id);
                game.Name = original?.Name ?? "Unknown Game";
            }

            if (!string.IsNullOrWhiteSpace(game.ExeName))
            {
                string trimmed = game.ExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    game.ExeName = trimmed[..^4].Trim();
                }
            }
            if (!string.IsNullOrWhiteSpace(game.InitExeName))
            {
                string trimmed = game.InitExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    game.InitExeName = trimmed[..^4].Trim();
                }
            }
            game.SaveFolderPath = game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.BackupFolderPath = game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.ImagePath = game.ImagePath?.Trim('"').Trim() ?? string.Empty;

            if (game.Score != 999)
            {
                if (game.Score < 1 || game.Score > 100)
                {
                    game.Score = 0; 
                 
                }
            }
            if (game.Excited != 999)
            {
                if (game.Excited < 1 || game.Excited > 100)
                {
                    game.Excited = 0;
                }
            }

            if (!string.IsNullOrWhiteSpace(game.FinishedIn))
            {
                string year = game.FinishedIn;
                string currentYear = DateTime.Now.ToString("yyyy");
                if (int.Parse(year) < 2003 || int.Parse(year) > (int.Parse(currentYear) + 1))
                {
                    game.FinishedIn = currentYear;
                }
            }
            return game;
        }

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

public static DateOnly? StringToDateOnly(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string value = input.Trim();

        string[] formats =
        {
        // dd/MM/yyyy, dd/MM/yy
        "d/M/yyyy", "dd/MM/yyyy",
        "d/M/yy",   "dd/MM/yy",

        // yy/MM/dd
        "yy/M/d",   "yy/MM/dd",

        // yyyy/MM/dd (bonus, tiện dùng)
        "yyyy/M/d", "yyyy/MM/dd",

        // dd-MM-yyyy, dd-MM-yy
        "d-M-yyyy", "dd-MM-yyyy",
        "d-M-yy",   "dd-MM-yy",

        // yy-MM-dd
        "yy-M-d",   "yy-MM-dd",

        // yyyy-MM-dd (bonus)
        "yyyy-M-d", "yyyy-MM-dd",

        "ddMMyyyy", "ddMMyy"
    };

        return DateOnly.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
            ? result
            : null;
    }
}
}