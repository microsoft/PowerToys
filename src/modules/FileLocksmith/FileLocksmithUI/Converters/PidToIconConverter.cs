// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;

using ManagedCommon;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PowerToys.FileLocksmithUI.Converters
{
    public sealed partial class PidToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var y = PowerToys.FileLocksmithLib.Interop.NativeMethods.PidToFullPath((uint)value);
            Icon icon = null;

            if (!string.IsNullOrEmpty(y))
            {
                try
                {
                    icon = Icon.ExtractAssociatedIcon(y);
                }
                catch (Exception ex)
                {
                    // The process image path can be non-empty but no longer exist on disk
                    // (e.g. self-updating software that deletes its old versioned directory while
                    // the old process is still running). ExtractAssociatedIcon then throws and,
                    // because this converter runs per-row during ListView virtualization, the
                    // exception would otherwise reach App_UnhandledException and fast-fail the app.
                    // Fall through to the placeholder icon instead of crashing.
                    Logger.LogWarning($"Couldn't extract the icon for '{y}'. {ex}");
                }
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
