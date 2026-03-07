using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MasterPlay.UI.Converters
{
    public class HexToGradientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Nếu value đã là Brush thì trả về chính nó (hỗ trợ fallback)
            if (value is Brush existingBrush)
                return existingBrush;

            // Nếu value là string hex, chuyển thành gradient
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    Color baseColor = (Color)ColorConverter.ConvertFromString(hex);
                    return CreateGlassGradient(baseColor);
                }
                catch
                {
                    // Nếu parse lỗi, trả về brush mặc định (xám trong suốt)
                    return CreateGlassGradient(Colors.Gray);
                }
            }

            // Mặc định: gradient xám
            return CreateGlassGradient(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private LinearGradientBrush CreateGlassGradient(Color baseColor)
        {
            // Tạo 3 màu biến thể:
            // - Màu sáng hơn (pha trắng 30%) với alpha 0.9
            // - Màu gốc với alpha 0.7
            // - Màu tối hơn (pha đen 30%) với alpha 0.8
            Color lightColor = Color.FromArgb(
                (byte)(255 * 0.9),
                (byte)Math.Min(255, baseColor.R + (255 - baseColor.R) * 0.3),
                (byte)Math.Min(255, baseColor.G + (255 - baseColor.G) * 0.3),
                (byte)Math.Min(255, baseColor.B + (255 - baseColor.B) * 0.3)
            );

            Color midColor = Color.FromArgb((byte)(255 * 0.7), baseColor.R, baseColor.G, baseColor.B);

            Color darkColor = Color.FromArgb(
                (byte)(255 * 0.8),
                (byte)(baseColor.R * 0.7),
                (byte)(baseColor.G * 0.7),
                (byte)(baseColor.B * 0.7)
            );

            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(lightColor, 0.0));
            gradient.GradientStops.Add(new GradientStop(midColor, 0.5));
            gradient.GradientStops.Add(new GradientStop(darkColor, 1.0));

            return gradient;
        }
    }
}