// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Microsoft.PowerToys.Common.UI.Controls
{
    // Reads the icon of a program and turns it into a picture that the interface can show.
    public static class IconLoader
    {
        // The size the icons are read in, so they all look the same in a list.
        public const int DefaultSize = 32;

        private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object CacheLock = new();

        // Nothing is returned when the file has no icon.
        public static async Task<ImageSource?> LoadAsync(string? path, int size = DefaultSize)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var key = $"{size}|{path}";

            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            ImageSource? source = null;

            try
            {
                var bits = IconReader.ReadIcon(path, size);
                if (bits != null)
                {
                    // The bitmap is left to the garbage collector, which frees it when the picture has been drawn.
                    var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                        bits.AsBuffer(),
                        BitmapPixelFormat.Bgra8,
                        size,
                        size,
                        BitmapAlphaMode.Premultiplied);

                    var loaded = new SoftwareBitmapSource();
                    await loaded.SetBitmapAsync(bitmap);
                    source = loaded;
                }
            }
            catch (Exception)
            {
                // The file cannot be read, so the program is shown without an icon.
                source = null;
            }

            lock (CacheLock)
            {
                Cache[key] = source;
            }

            return source;
        }
    }
}
