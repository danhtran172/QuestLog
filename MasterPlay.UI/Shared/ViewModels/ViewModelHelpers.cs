using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Core.Entities;
using MasterPlay.Core.Localization;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.Shared.ViewModels
{
    public static class ViewModelHelpers
    {
        public static Game InputFormatter(Game game)
        {
            if (game == null) return game;

            if (string.IsNullOrWhiteSpace(game.Name))
            {
                var original = DataService.LoadGames().FirstOrDefault(g => g.Id == game.Id);
                game.Name = original?.Name ?? LocalizationService.Get("UnknownGame");
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

            game.Score = ScaleAndRound(game.Score);
            game.Excited = ScaleAndRound(game.Excited);

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

        private static double? ScaleAndRound(double? value)
        {
            if (!value.HasValue || value.Value <= 0)
                return null;

            if ((int)value.Value == 999)
                return value;

            double score = value.Value;
            while (score > 10)
            {
                score /= 10;
            }
            return Math.Round(score *2, MidpointRounding.AwayFromZero) /2;
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

        "d/M/yyyy", "dd/MM/yyyy",
        "d/M/yy",   "dd/MM/yy",


        "yy/M/d",   "yy/MM/dd",


        "yyyy/M/d", "yyyy/MM/dd",


        "d-M-yyyy", "dd-MM-yyyy",
        "d-M-yy",   "dd-MM-yy",


        "yy-M-d",   "yy-MM-dd",


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

        public static int GetFinishedYear(string finishedIn)
        {
            if (string.IsNullOrWhiteSpace(finishedIn))
                return 0;
            if (int.TryParse(finishedIn, out int year))
                return year;
            if (finishedIn.Length >= 4 && int.TryParse(finishedIn.Substring(0, 4), out year))
                return year;
            return 0;
        }

    }
}
