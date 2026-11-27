// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using WorkspacesCsharpLibrary.Models;

namespace WorkspacesEditor.Helpers
{
    internal static class IconHelper
    {
        public static BitmapImage TryGetApplicationIcon(BaseApplication application)
        {
            if (application == null)
            {
                return null;
            }

            try
            {
                // BaseApplication owns the cached icon and resolves PWA and packaged-app logos.
                Icon icon = application.Icon;
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
                                    or InvalidOperationException
                                    or COMException
                                    or IOException)
            {
                return null;
            }
        }
    }
}
