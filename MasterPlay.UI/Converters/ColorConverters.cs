using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace MasterPlay.UI.Converters
{
    public class SelectionColorConverter : IValueConverter
    {
        private static List<OwnershipStatus>? _ownershipCache;
        private static List<ProgressStatus>? _progressCache;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var directColorCode = value switch
            {
                OwnershipStatus ownership => ownership.ColorCode,
                ProgressStatus progress => progress.ColorCode,
                _ => null
            };
            var directBrush = CreateBrush(directColorCode);
            if (directBrush != null)
                return directBrush;

            int? id = null;
            if (value is int intVal) id = intVal;
            else if (value is OwnershipStatus os) id = os.Id;
            else if (value is ProgressStatus ps) id = ps.Id;
            else if (value is string str && int.TryParse(str, out int parsedId)) id = parsedId;

            if (id.HasValue && id > 0)
            {
                if (parameter as string == "Progress")
                {
                    _progressCache ??= DataService.LoadProgressList();
                    var progress = _progressCache.FirstOrDefault(p => p.Id == id);
                    var brush = CreateBrush(progress?.ColorCode);
                    if (brush != null)
                        return brush;
                }
                else if (parameter as string == "Ownership")
                {
                    _ownershipCache ??= DataService.LoadOwnershipList();
                    var ownership = _ownershipCache.FirstOrDefault(o => o.Id == id);
                    var brush = CreateBrush(ownership?.ColorCode);
                    if (brush != null)
                        return brush;
                }
            }
            return Brushes.Transparent;
        }

        private static SolidColorBrush? CreateBrush(string? colorCode)
        {
            if (string.IsNullOrWhiteSpace(colorCode))
                return null;

            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class SelectionGlassColorConverter : IValueConverter
    {
        private static List<OwnershipStatus>? _ownershipCache;
        private static List<ProgressStatus>? _progressCache;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var directColorCode = value switch
            {
                OwnershipStatus ownership => ownership.ColorCode,
                ProgressStatus progress => progress.ColorCode,
                _ => null
            };
            var directBrush = CreateGlassBrush(directColorCode);
            if (directBrush != null)
                return directBrush;

            int? id = null;
            if (value is int intVal) id = intVal;
            else if (value is OwnershipStatus os) id = os.Id;
            else if (value is ProgressStatus ps) id = ps.Id;
            else if (value is string str && int.TryParse(str, out int parsedId)) id = parsedId;
            if (id.HasValue && id > 0)
            {
                if (parameter as string == "Progress")
                {
                    _progressCache ??= DataService.LoadProgressList();
                    var progress = _progressCache.FirstOrDefault(p => p.Id == id);
                    var brush = CreateGlassBrush(progress?.ColorCode);
                    if (brush != null)
                        return brush;
                }
                else if (parameter as string == "Ownership")
                {
                    _ownershipCache ??= DataService.LoadOwnershipList();
                    var ownership = _ownershipCache.FirstOrDefault(o => o.Id == id);
                    var brush = CreateGlassBrush(ownership?.ColorCode);
                    if (brush != null)
                        return brush;
                }
            }
            return Brushes.Transparent;
        }

        private static SolidColorBrush? CreateGlassBrush(string? colorCode)
        {
            if (string.IsNullOrWhiteSpace(colorCode))
                return null;

            var glassColor = colorCode.Replace("#", "#40");
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(glassColor));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class MetaScoreColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                var converter = new ColorConverter();

                if (score >= 75) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00CE7A"));
                if (score >= 50) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFBD3F"));
                if (score > 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6874"));

                return new SolidColorBrush(Color.FromArgb(0, 255, 189, 63));
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
