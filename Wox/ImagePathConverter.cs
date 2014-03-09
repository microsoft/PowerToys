using System;
using System.Collections.Generic;
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
        private static Dictionary<string, object> imageCache = new Dictionary<string, object>();

        private static ImageSource GetIcon(string fileName)
        {
            Icon icon = Icon.ExtractAssociatedIcon(fileName);
            if (icon != null)
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, new Int32Rect(0, 0, icon.Width, icon.Height), BitmapSizeOptions.FromEmptyOptions());
            }

            return null;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            object img = null;
            if (values[0] == null) return null;

            string path = values[0].ToString();
            string pluginDirectory = values[1].ToString();
            string fullPath = Path.Combine(pluginDirectory, path);
            if (imageCache.ContainsKey(fullPath))
            {
                return imageCache[fullPath];
            }

            string resolvedPath = string.Empty;
            if (!string.IsNullOrEmpty(path) && path.Contains(":\\") && File.Exists(path))
            {
                resolvedPath = path;
            }
            else if (!string.IsNullOrEmpty(path) && File.Exists(fullPath))
            {
                resolvedPath = fullPath;
            }

            if (resolvedPath.ToLower().EndsWith(".exe") || resolvedPath.ToLower().EndsWith(".lnk"))
            {
                img = GetIcon(resolvedPath);
            }
            else if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                img  = new BitmapImage(new Uri(resolvedPath));
            }

            if (img != null)
            {
                imageCache.Add(fullPath, img);
            }

            return img;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}