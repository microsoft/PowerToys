// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace SamplePagesExtension.Pages;

/// <summary>
/// Builds a data-backed icon of a controlled size, so a list can exercise the
/// stream-reference path rather than the cheap glyph path.
/// </summary>
/// <remarks>
/// A glyph icon costs the host nothing - <c>IIconData.Data</c> stays null and no
/// proxy is taken. An icon with real data makes the host hold an
/// <c>ExtensionObject&lt;IRandomAccessStreamReference&gt;</c> per item, which
/// pins the bytes on this side until the host releases it. That is the path this
/// scenario is meant to measure.
/// </remarks>
internal static class BallastIcon
{
    private const int BmpHeaderSize = 54;

    /// <summary>
    /// Creates an icon backed by roughly <c>side * side * 4</c> bytes of image data.
    /// </summary>
    public static IconInfo Create(int side, int seed)
    {
        var bytes = CreateBitmap(side, seed);

        var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream);
        try
        {
            writer.WriteBytes(bytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.DetachStream();
        }
        finally
        {
            writer.Dispose();
        }

        stream.Seek(0);

        // Light and Dark deliberately share one IconData, which is the common
        // case. The host still creates a separate IconDataViewModel for each,
        // so it ends up holding two proxies onto this one object.
        return new IconInfo(new IconData(new TrackedStreamReference(stream, bytes.Length)));
    }

    /// <summary>
    /// A minimal 32bpp BMP. Chosen over PNG because the size is exactly
    /// predictable from the side length and it needs no encoder.
    /// </summary>
    private static byte[] CreateBitmap(int side, int seed)
    {
        var pixelBytes = side * side * 4;
        var bytes = new byte[BmpHeaderSize + pixelBytes];

        // BITMAPFILEHEADER
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteInt32(bytes, 2, bytes.Length);
        WriteInt32(bytes, 10, BmpHeaderSize);

        // BITMAPINFOHEADER
        WriteInt32(bytes, 14, 40);
        WriteInt32(bytes, 18, side);
        WriteInt32(bytes, 22, side);
        bytes[26] = 1; // colour planes
        bytes[28] = 32; // bits per pixel
        WriteInt32(bytes, 34, pixelBytes);

        // Noise rather than a solid fill, so nothing downstream can collapse
        // these buffers and hide the allocation.
        new Random(seed).NextBytes(bytes.AsSpan(BmpHeaderSize));

        return bytes;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
