// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace PowerToys.FileLocksmithUI.Converters
{
    public sealed class PidToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var y = FileLocksmith.Interop.NativeMethods.PidToFullPath((uint)value);
            Icon icon = null;

            if (!string.IsNullOrEmpty(y))
            {
                icon = Icon.ExtractAssociatedIcon(y);
            }

            if (icon != null)
            {
                Bitmap bitmap = icon.ToBitmap();
                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            else
            {
                return new BitmapImage();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
