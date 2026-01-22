// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal static class ImageMetadataAnalyzer
{
    /// <summary>
    /// Reads image metadata from a RandomAccessStreamReference without decoding pixels.
    /// Returns oriented dimensions (EXIF rotation applied).
    /// </summary>
    public static async Task<ImageMetadata> GetAsync(RandomAccessStreamReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        using IRandomAccessStream ras = await reference.OpenReadAsync().AsTask().ConfigureAwait(false);
        var sizeBytes = TryGetSize(ras);

        // BitmapDecoder does not decode pixel data unless you ask it to,
        // so this is fast and memory-friendly.
        var decoder = await BitmapDecoder.CreateAsync(ras).AsTask().ConfigureAwait(false);

        // OrientedPixelWidth/Height account for EXIF orientation
        var width = decoder.OrientedPixelWidth;
        var height = decoder.OrientedPixelHeight;

        return new ImageMetadata(
            Width: width,
            Height: height,
            DpiX: decoder.DpiX,
            DpiY: decoder.DpiY,
            StorageSize: sizeBytes);
    }

    private static ulong? TryGetSize(IRandomAccessStream s)
    {
        try
        {
            // On file-backed streams this is accurate.
            // On some URI/virtual streams this may be unsupported or 0.
            var size = s.Size;
            return size == 0 ? (ulong?)0 : size;
        }
        catch
        {
            return null;
        }
    }
}
