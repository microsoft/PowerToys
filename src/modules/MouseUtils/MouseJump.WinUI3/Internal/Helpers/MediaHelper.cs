// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml.Media.Imaging;

namespace MouseJump.WinUI3.Internal.Helpers;

internal static class MediaHelper
{
    /// <summary>
    /// Copies <paramref name="bitmap"/>'s pixels directly into a <see cref="WriteableBitmap"/>,
    /// rather than round-tripping through a PNG encode (GDI+) + decode (WinUI). Works as a
    /// straight byte copy because <see cref="DrawingHelper.RenderBorder"/> always produces
    /// <see cref="PixelFormat.Format32bppPArgb"/>, which is byte-for-byte the same layout as
    /// <see cref="WriteableBitmap"/>'s own pixel buffer (BGRA8, premultiplied).
    /// </summary>
    internal static WriteableBitmap ToBitmapImage(Bitmap bitmap)
    {
        var writeableBitmap = new WriteableBitmap(bitmap.Width, bitmap.Height);

        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var byteCount = bitmapData.Stride * bitmapData.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(bitmapData.Scan0, buffer, 0, byteCount);

            using var pixelStream = writeableBitmap.PixelBuffer.AsStream();
            pixelStream.Write(buffer, 0, byteCount);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        writeableBitmap.Invalidate();
        return writeableBitmap;
    }
}
