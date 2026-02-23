using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.Converters
{
    public class SteamImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;

            // Thay 'MasterPlay.UI' bằng đúng tên Project của bạn (nhìn ở Solution Explorer)
            var defaultUri = new Uri("pack://application:,,,/MasterPlay.UI;component/Assets/NoImage1.png", UriKind.Absolute);

            if (string.IsNullOrWhiteSpace(path))
                return new BitmapImage(defaultUri);

            try
            {
                // Kiểm tra xem path có phải là URL hợp lệ không
                if (!Uri.TryCreate(path, UriKind.Absolute, out Uri resultUri))
                {
                    return new BitmapImage(defaultUri);
                }

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = resultUri;
                bi.DecodePixelWidth = 640;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.EndInit();
                return bi;
            }
            catch
            {
                // Mọi lỗi phát sinh (404, mất mạng, file hỏng) đều trả về ảnh mặc định
                return new BitmapImage(defaultUri);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}