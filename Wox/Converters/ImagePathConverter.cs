using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Wox.Helper;

namespace Wox.Converters
{
    public class ImagePathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null) return null;

            string relativePath = values[0].ToString();
            string pluginDirectory = values[1].ToString();
            string fullPath = Path.Combine(pluginDirectory, relativePath);

            if (relativePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return new BitmapImage(new Uri(relativePath));
            }
            return ImageLoader.Load(fullPath);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}