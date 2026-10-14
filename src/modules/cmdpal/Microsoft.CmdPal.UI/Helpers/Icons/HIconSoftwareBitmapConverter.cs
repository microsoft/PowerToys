// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using DrawingIcon = System.Drawing.Icon;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class HIconSoftwareBitmapConverter
{
    /// <summary>
    /// Converts an owned HICON into a BGRA8 premultiplied bitmap and always destroys the handle.
    /// </summary>
    public static SoftwareBitmap? ConvertAndDestroy(nint iconHandle)
    {
        if (iconHandle == 0)
        {
            return null;
        }

        try
        {
            using var icon = DrawingIcon.FromHandle(iconHandle);
            using var sourceBitmap = icon.ToBitmap();
            using var bitmap = sourceBitmap.Clone(
                new DrawingRectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                DrawingPixelFormat.Format32bppPArgb);

            var rectangle = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(
                rectangle,
                DrawingImageLockMode.ReadOnly,
                DrawingPixelFormat.Format32bppPArgb);
            try
            {
                var bytesPerRow = checked(bitmap.Width * 4);
                var pixelBufferLength = checked(bytesPerRow * bitmap.Height);
                var pixels = ArrayPool<byte>.Shared.Rent(pixelBufferLength);
                try
                {
                    if (bitmapData.Stride == bytesPerRow)
                    {
                        Marshal.Copy(bitmapData.Scan0, pixels, 0, pixelBufferLength);
                    }
                    else
                    {
                        for (var row = 0; row < bitmap.Height; row++)
                        {
                            var source = nint.Add(bitmapData.Scan0, row * bitmapData.Stride);
                            Marshal.Copy(source, pixels, row * bytesPerRow, bytesPerRow);
                        }
                    }

                    var softwareBitmap = new SoftwareBitmap(
                        BitmapPixelFormat.Bgra8,
                        bitmap.Width,
                        bitmap.Height,
                        BitmapAlphaMode.Premultiplied);
                    try
                    {
                        softwareBitmap.CopyFromBuffer(pixels.AsBuffer(0, pixelBufferLength));
                        return softwareBitmap;
                    }
                    catch
                    {
                        softwareBitmap.Dispose();
                        throw;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(pixels);
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            _ = NativeMethods.DestroyIcon(iconHandle);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int DestroyIcon(nint icon);
    }
}
