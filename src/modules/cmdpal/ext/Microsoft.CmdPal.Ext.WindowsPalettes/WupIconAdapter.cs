// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    internal static IconInfo Create(PaletteIconData? icon)
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
            return FromBitmap(bitmap) ?? new IconInfo(string.Empty);
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

    private static IconInfo? FromBitmap(SoftwareBitmap bitmap)
    {
        try
        {
            var encode = Task.Run(async () =>
            {
                var source = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                    bitmap.BitmapAlphaMode != BitmapAlphaMode.Straight
                        ? bitmap
                        : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(source);
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
}
