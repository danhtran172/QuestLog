using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.Converters
{
    public class SteamImageConverter : IValueConverter
    {

        private static readonly Dictionary<string, BitmapImage> ImageCache = new Dictionary<string, BitmapImage>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;


            var defaultUri = new Uri("pack://application:,,,/MasterPlay.UI;component/Assets/NoImage1.png", UriKind.Absolute);
            var defaultImage = GetCachedImage(defaultUri.ToString());

            if (string.IsNullOrWhiteSpace(path))
                return defaultImage;

            try
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out Uri resultUri))
                    return defaultImage;


                string cacheKey = resultUri.ToString();
                if (ImageCache.TryGetValue(cacheKey, out BitmapImage cachedImage))
                    return cachedImage;


                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = resultUri;
                bi.DecodePixelWidth = 460;
                bi.CacheOption = BitmapCacheOption.OnLoad;

                bi.EndInit();


                ImageCache[cacheKey] = bi;

                return bi;
            }
            catch
            {
                return defaultImage;
            }
        }

        private BitmapImage GetCachedImage(string uri)
        {
            if (ImageCache.TryGetValue(uri, out BitmapImage img))
                return img;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(uri, UriKind.Absolute);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            ImageCache[uri] = bi;
            return bi;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}