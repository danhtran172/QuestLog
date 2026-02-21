using MasterPlay.Models;
using MasterPlay.UI.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
namespace MasterPlay.UI.Converters
{
    public class SelectionColorConverter : IValueConverter
{
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string selectionName && selectionName != "--")
            {
                Dictionary<string, string>? colors = null;
                if (parameter as string == "Progress")
                    colors = DataService.Progress;
                else if (parameter as string == "Ownership")
                    colors = DataService.Ownership;

                if (colors != null && colors.TryGetValue(selectionName, out var hex))
                {
                    try
                    {
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                    }
                    catch { }
                }
            }
            return Brushes.Transparent; // Không màu nếu null hoặc "--"
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}