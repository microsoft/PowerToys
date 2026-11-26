// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

// Command Palette icons have no font-family field, so PaletteIconData.FontFamily can't be passed through;
// glyphs render with Command Palette's default icon font. IconOverlay has no Command Palette equivalent.
internal static class WupIconAdapter
{
    private static readonly TimeSpan BitmapEncodeTimeout = TimeSpan.FromSeconds(2);

    internal static IconInfo Create(PaletteIconData? icon, bool trimTransparentPadding = false)
    {
        if (icon is null)
        {
            return new IconInfo(string.Empty);
        }

        if (!string.IsNullOrEmpty(icon.IconGlyph))
        {
            return new IconInfo(icon.IconGlyph);
        }

        if (icon.IconUri is not null)
        {
            return new IconInfo(icon.IconUri.ToString());
        }

        if (icon.ImageStreamReference is not null)
        {
            return new IconInfo(new IconData(icon.ImageStreamReference));
        }

        if (TryGetBitmap(icon) is { } bitmap)
        {
            return FromBitmap(bitmap, trimTransparentPadding) ?? new IconInfo(string.Empty);
        }

        return new IconInfo(string.Empty);
    }

    private static SoftwareBitmap? TryGetBitmap(PaletteIconData icon)
    {
        try
        {
            return icon.Bitmap;
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Failed to read bitmap icon: 0x{ex.HResult:X8} {ex.Message}");
            return null;
        }
    }

    internal static IconInfo? FromBitmap(SoftwareBitmap bitmap, bool trimTransparentPadding)
    {
        try
        {
            var encode = Task.Run(async () =>
            {
                using var converted = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                    bitmap.BitmapAlphaMode != BitmapAlphaMode.Straight
                        ? null
                        : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var source = converted ?? bitmap;
                var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(source);
                if (trimTransparentPadding)
                {
                    encoder.BitmapTransform.Bounds = GetContentBounds(source);
                }

                await encoder.FlushAsync();
                stream.Seek(0);
                return new IconInfo(new IconData(RandomAccessStreamReference.CreateFromStream(stream)));
            });
            return encode.Wait(BitmapEncodeTimeout) ? encode.Result : null;
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Failed to convert bitmap icon: 0x{ex.HResult:X8} {ex.Message}");
            return null;
        }
    }

    private static BitmapBounds GetContentBounds(SoftwareBitmap bitmap)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        if (bitmap.BitmapAlphaMode == BitmapAlphaMode.Ignore)
        {
            return new BitmapBounds { Width = (uint)width, Height = (uint)height };
        }

        var pixels = new byte[checked(width * height * 4)];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(((y * width) + x) * 4) + 3] == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < 0
            ? new BitmapBounds { Width = (uint)width, Height = (uint)height }
            : new BitmapBounds
            {
                X = (uint)minX,
                Y = (uint)minY,
                Width = (uint)(maxX - minX + 1),
                Height = (uint)(maxY - minY + 1),
            };
    }
}
