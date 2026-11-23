// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class BitmapPixels : IDisposable
{
    private byte[]? pixels;

    internal BitmapPixels(Bitmap bitmap, Rectangle? region = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var area = region ?? new Rectangle(Point.Empty, bitmap.Size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.Height);
        Width = area.Width;
        Height = area.Height;
        var rowBytes = checked(Width * 4);
        var buffer = ArrayPool<byte>.Shared.Rent(checked(rowBytes * Height));
        try
        {
            var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                // Copy logical rows individually: Scan0 need not have a positive stride.
                for (var y = 0; y < Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, checked(y * data.Stride)), buffer, y * rowBytes, rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            pixels = buffer;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    internal int Width { get; }

    internal int Height { get; }

    internal Color GetColor(int x, int y)
    {
        var buffer = pixels ?? throw new ObjectDisposedException(nameof(BitmapPixels));
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        var offset = ((y * Width) + x) * 4;
        return Color.FromArgb(buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset]);
    }

    public void Dispose()
    {
        if (pixels is { } buffer)
        {
            pixels = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
