// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    // Turns a registry icon location ("path", "path,index", "\"path\",-resId") into PNG bytes.
    // Pure Win32 + System.Drawing, so it can run off the UI thread; the caller builds the
    // BitmapImage on the UI thread.
    internal static class ShellIconHelper
    {
        public static byte[] ExtractPng(string iconLocation, int size)
        {
            if (!TryParseLocation(iconLocation, out string path, out int index))
            {
                return null;
            }

            if (NativeMethods.SHDefExtractIcon(path, index, 0, out IntPtr hIcon, IntPtr.Zero, (uint)size) != 0 || hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = Icon.FromHandle(hIcon);
                using var bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }

        private static bool TryParseLocation(string iconLocation, out string path, out int index)
        {
            path = null;
            index = 0;
            if (string.IsNullOrWhiteSpace(iconLocation))
            {
                return false;
            }

            string location = iconLocation.Trim().TrimStart('@');

            // The index follows the last comma, but only when that tail is a number: paths may contain commas.
            int comma = location.LastIndexOf(',');
            if (comma > 0 && int.TryParse(location.AsSpan(comma + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                index = parsed;
                location = location.Substring(0, comma);
            }

            path = Environment.ExpandEnvironmentVariables(location.Trim().Trim('"'));
            return path.Length > 0;
        }
    }
}
