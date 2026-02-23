// MetaScoreColorConverter.cs (sửa cho MetaScore là int)
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MasterPlay.UI.Converters
{
    public class MetaScoreColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                if (score >= 75) return new SolidColorBrush(Color.FromRgb(0, 206, 122));   
                if (score >= 50) return new SolidColorBrush(Color.FromRgb(255, 189, 63)); 
                if (score > 0) return new SolidColorBrush(Color.FromRgb(255, 104, 116)); 
                
                return new SolidColorBrush(Color.FromArgb(0, 255, 189, 63));                                 
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}