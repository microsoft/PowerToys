// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;

using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Core.Models;

namespace PowerOCR.Models;

/// <summary>
/// Owns a full-screen capture for a single display, including the GDI+ bitmap
/// and a WinUI-compatible <see cref="SoftwareBitmapSource"/>.
/// </summary>
public sealed class DisplayCapture : IDisposable
{
    private bool _disposed;

    public DisplayCapture(DisplayBounds bounds, Bitmap bitmap, SoftwareBitmapSource imageSource)
    {
        Bounds = bounds;
        Bitmap = bitmap;
        ImageSource = imageSource;
    }

    public DisplayBounds Bounds { get; }

    public Bitmap Bitmap { get; }

    public SoftwareBitmapSource ImageSource { get; }

    /// <summary>
    /// Crops the captured bitmap to the specified local rectangle.
    /// Coordinates are relative to this display (0,0 is the top-left of the captured bitmap).
    /// </summary>
    public Bitmap Crop(PixelRect localRect)
    {
        int x = Math.Max(0, localRect.X);
        int y = Math.Max(0, localRect.Y);
        int width = Math.Min(localRect.Width, Bitmap.Width - x);
        int height = Math.Min(localRect.Height, Bitmap.Height - y);

        if (width <= 0 || height <= 0)
        {
            return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        }

        return Bitmap.Clone(
            new Rectangle(x, y, width, height),
            PixelFormat.Format32bppArgb);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Bitmap.Dispose();
        ImageSource.Dispose();
    }
}
