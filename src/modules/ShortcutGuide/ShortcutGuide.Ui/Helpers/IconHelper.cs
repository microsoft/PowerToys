// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Helpers for loading executable icons as XAML image sources.
    /// </summary>
    internal static class IconHelper
    {
        /// <summary>
        /// Extracts the associated icon of an executable file and returns it as a
        /// <see cref="BitmapImage"/> suitable for use with <c>ImageIcon</c>.
        /// </summary>
        /// <param name="path">Full path to the executable, or <c>null</c>/empty.</param>
        /// <returns>
        /// A <see cref="BitmapImage"/> when extraction succeeds, otherwise <c>null</c>.
        /// Callers should fall back to a glyph icon when this returns <c>null</c>.
        /// </returns>
        public static BitmapImage? TryGetExecutableIcon(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(path);
                if (icon is null)
                {
                    return null;
                }

                using Bitmap bitmap = icon.ToBitmap();
                using MemoryStream stream = new();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                BitmapImage bitmapImage = new();
                bitmapImage.SetSource(stream.AsRandomAccessStream());
                return bitmapImage;
            }
            catch (Exception ex) when (ex is FileNotFoundException
                                    or UnauthorizedAccessException
                                    or Win32Exception
                                    or ArgumentException
                                    or IOException)
            {
                return null;
            }
        }
    }
}
