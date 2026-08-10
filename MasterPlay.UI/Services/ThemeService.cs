using System.Windows;
using System.Windows.Media;

namespace MasterPlay.UI.Services;

public static class ThemeService
{
    public static void Apply(bool isDarkMode)
    {
        var colors = isDarkMode
            ? new Dictionary<string, string>
            {
                ["WindowBackgroundBrush"] = "#171B22",
                ["SurfaceBrush"] = "#222833",
                ["ElevatedSurfaceBrush"] = "#2A313D",
                ["InputBackgroundBrush"] = "#252C36",
                ["ReadOnlyInputBackgroundBrush"] = "#202731",
                ["MainTextBrush"] = "#F1F5F9",
                ["FieldTextBrush"] = "#C4CBD5",
                ["SubTextBrush"] = "#9DA8B8",
                ["BorderBrush"] = "#3D4756",
                ["SoftBorderBrush"] = "#465160",
                ["ItemHoverBackground"] = "#303946",
                ["ItemSelectedBackground"] = "#294B6A"
            }
            : new Dictionary<string, string>
            {
                ["WindowBackgroundBrush"] = "#F8F9FA",
                ["SurfaceBrush"] = "#FFFFFF",
                ["ElevatedSurfaceBrush"] = "#FAFAFA",
                ["InputBackgroundBrush"] = "#FFFFFF",
                ["ReadOnlyInputBackgroundBrush"] = "#F9F9F9",
                ["MainTextBrush"] = "#2C3E50",
                ["FieldTextBrush"] = "#555555",
                ["SubTextBrush"] = "#7F8C8D",
                ["BorderBrush"] = "#DDDDDD",
                ["SoftBorderBrush"] = "#E0E0E0",
                ["ItemHoverBackground"] = "#ECECEC",
                ["ItemSelectedBackground"] = "#D2E3FC"
            };

        foreach (var (key, color) in colors)
            Application.Current.Resources[key] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color));
    }
}
