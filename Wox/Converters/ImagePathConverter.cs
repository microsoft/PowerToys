using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Wox.Helper;

namespace Wox.Converters
{
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DependencyProperty.UnsetValue) return null;

            string fullPath = value.ToString();

            if (fullPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return new BitmapImage(new Uri(fullPath));
            }

            
            return ImageLoader.Load(fullPath);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}