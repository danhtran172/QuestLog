using MasterPlay.Models;
using MasterPlay.Models.Models;
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
                    if (progress?.ColorCode != null)
                    {
                        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(progress.ColorCode)); }
                        catch { }
                    }
                }
                else if (parameter as string == "Ownership")
                {
                    _ownershipCache ??= DataService.LoadOwnershipList();
                    var ownership = _ownershipCache.FirstOrDefault(o => o.Id == id);
                    if (ownership?.ColorCode != null)
                    {
                        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ownership.ColorCode)); }
                        catch { }
                    }
                }
            }
            return Brushes.Transparent;
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
            int? id = null;
            if (value is int intVal) id = intVal;
            else if (value is OwnershipStatus os) id = os.Id;
            else if (value is ProgressStatus ps) id = ps.Id;
            else if (value is string str && int.TryParse(str, out int parsedId)) id = parsedId;
            var opacity = "40"; //%
            if (id.HasValue && id > 0)
            {
                if (parameter as string == "Progress")
                {
                    _progressCache ??= DataService.LoadProgressList();
                    var progress = _progressCache.FirstOrDefault(p => p.Id == id);
                    if (progress?.ColorCode != null)
                    {
                        var glassColor = progress.ColorCode.Replace("#", $"#{opacity}");
                        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(glassColor)); }
                        catch { }
                    }
                }
                else if (parameter as string == "Ownership")
                {
                    _ownershipCache ??= DataService.LoadOwnershipList();
                    var ownership = _ownershipCache.FirstOrDefault(o => o.Id == id);
                    if (ownership?.ColorCode != null)
                    {
                        
                        var glassColor = ownership.ColorCode.Replace("#", $"#{opacity}");
                        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(glassColor)); }
                        catch { }
                    }
                }
            }
            return Brushes.Transparent;
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