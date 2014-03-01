using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wox
{
    public class ImagePathConverter : IMultiValueConverter
    {
        private static ImageSource GetIcon(string fileName)
        {
            Icon icon = Icon.ExtractAssociatedIcon(fileName);
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                new Int32Rect(0, 0, icon.Width, icon.Height),
                BitmapSizeOptions.FromEmptyOptions());
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null) return null;

            string path = values[0].ToString();
            string pluginDirectory = values[1].ToString();

            string resolvedPath = string.Empty;
            if (!string.IsNullOrEmpty(path) && path.Contains(":\\") && File.Exists(path))
            {
                resolvedPath = path;
            }
            else if (!string.IsNullOrEmpty(path) && File.Exists(Path.Combine(pluginDirectory,path)))
            {
                resolvedPath = Path.Combine(pluginDirectory, path);
            }

            if (resolvedPath.ToLower().EndsWith(".exe") || resolvedPath.ToLower().EndsWith(".lnk"))
            {
                return GetIcon(resolvedPath);
            }

            if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                return new BitmapImage(new Uri(resolvedPath));
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}