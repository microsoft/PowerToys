// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PowerOCR.Services;

internal sealed class ScreenCaptureService : IScreenCaptureService
{
    public async Task<DisplayCapture> CaptureAsync(DisplayArea display, CancellationToken cancellationToken)
    {
        var outerBounds = display.OuterBounds;
        var bounds = new DisplayBounds(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);

        Bitmap? bitmap = null;
        SoftwareBitmapSource? source = null;
        SoftwareBitmap? softwareBitmap = null;
        DisplayCapture? capture = null;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Capture the display using GDI+
            bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Convert to SoftwareBitmapSource for WinUI display
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Position = 0;

            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                await RandomAccessStream.CopyAsync(memoryStream.AsInputStream(), outputStream);
                await outputStream.FlushAsync();
            }

            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);

            capture = new DisplayCapture(bounds, bitmap, source);
            return capture;
        }
        finally
        {
            softwareBitmap?.Dispose();

            if (capture is null)
            {
                source?.Dispose();
                bitmap?.Dispose();
            }
        }
    }
}
