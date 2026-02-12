using System;
using System.Globalization;
using System.Windows.Data;

namespace MasterPlay.UI.Converters
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isAccessible ? (isAccessible ? 1.0 : 0.5) : 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}