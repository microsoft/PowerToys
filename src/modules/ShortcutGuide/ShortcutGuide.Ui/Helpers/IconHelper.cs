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

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Helpers for loading executable and window icons as XAML image sources.
    /// </summary>
    internal static class IconHelper
    {
        /// <summary>
        /// Extracts the associated icon of an executable file and returns it as a
        /// <see cref="BitmapImage"/> suitable for use with <c>ImageIcon</c>.
        /// </summary>
        /// <param name="path">Full path to the executable, or <c>null</c>/empty.</param>
        /// <returns>
        /// A <see cref="BitmapImage"/> when extraction succeeds; otherwise <c>null</c>.
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
                return icon is null ? null : ToBitmapImage(icon);
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

        /// <summary>
        /// Gets the icon a window assigned to itself through <c>WM_SETICON</c> and returns it as a
        /// <see cref="BitmapImage"/> suitable for use with <c>ImageIcon</c>.
        /// </summary>
        /// <remarks>
        /// Browser-hosted web apps run in the browser executable but set their own app icon on
        /// their windows, so the window icon is more accurate than the executable icon for them.
        /// </remarks>
        /// <param name="windowHandle">The window handle.</param>
        /// <returns>
        /// A <see cref="BitmapImage"/> when the window has its own icon; otherwise <c>null</c>.
        /// </returns>
        public static BitmapImage? TryGetWindowIcon(nint windowHandle)
        {
            if (windowHandle == 0)
            {
                return null;
            }

            nint iconHandle = GetWindowIconHandle(windowHandle, NativeMethods.ICON_BIG);
            if (iconHandle == 0)
            {
                iconHandle = GetWindowIconHandle(windowHandle, NativeMethods.ICON_SMALL2);
            }

            if (iconHandle == 0)
            {
                return null;
            }

            try
            {
                // The window owns the icon handle, so the Icon must not destroy it.
                using Icon icon = Icon.FromHandle(iconHandle);
                return ToBitmapImage(icon);
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException)
            {
                return null;
            }
        }

        private static nint GetWindowIconHandle(nint windowHandle, int iconType)
        {
            // Use a timeout so a hung window cannot block the UI thread.
            return NativeMethods.SendMessageTimeoutW(windowHandle, NativeMethods.WM_GETICON, iconType, 0, NativeMethods.SMTO_ABORTIFHUNG, 100, out nint result) == 0
                ? 0
                : result;
        }

        private static BitmapImage ToBitmapImage(Icon icon)
        {
            using Bitmap bitmap = icon.ToBitmap();
            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmapImage.SetSource(stream.AsRandomAccessStream());
            return bitmapImage;
        }
    }
}
