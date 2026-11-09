// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class ShellIconExtractionResult : IDisposable
{
    private SoftwareBitmap? _softwareBitmap;
    private IRandomAccessStream? _bitmapStream;

    private ShellIconExtractionResult(
        SoftwareBitmap? softwareBitmap = null,
        IRandomAccessStream? bitmapStream = null,
        ShellImageListSize? imageListSize = null,
        int requestedPixelSize = 0,
        int sourceWidth = 0,
        int sourceHeight = 0,
        long hIconConversionTicks = 0)
    {
        _softwareBitmap = softwareBitmap;
        _bitmapStream = bitmapStream;
        ImageListSize = imageListSize;
        RequestedPixelSize = requestedPixelSize;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        HIconConversionTicks = hIconConversionTicks;
    }

    public SoftwareBitmap? SoftwareBitmap => _softwareBitmap;

    public IRandomAccessStream? BitmapStream => _bitmapStream;

    public ShellImageListSize? ImageListSize { get; }

    public int RequestedPixelSize { get; }

    public int SourceWidth { get; }

    public int SourceHeight { get; }

    public long HIconConversionTicks { get; }

    public bool HasContent => _softwareBitmap is not null || _bitmapStream is not null;

    public static ShellIconExtractionResult Empty(
        ShellImageListSize? imageListSize = null,
        int requestedPixelSize = 0,
        int sourceWidth = 0,
        int sourceHeight = 0,
        long hIconConversionTicks = 0) =>
        new(
            imageListSize: imageListSize,
            requestedPixelSize: requestedPixelSize,
            sourceWidth: sourceWidth,
            sourceHeight: sourceHeight,
            hIconConversionTicks: hIconConversionTicks);

    public static ShellIconExtractionResult FromSoftwareBitmap(
        SoftwareBitmap softwareBitmap,
        ShellImageListSize imageListSize,
        int requestedPixelSize,
        int sourceWidth,
        int sourceHeight,
        long hIconConversionTicks) =>
        new(
            softwareBitmap: softwareBitmap,
            imageListSize: imageListSize,
            requestedPixelSize: requestedPixelSize,
            sourceWidth: sourceWidth,
            sourceHeight: sourceHeight,
            hIconConversionTicks: hIconConversionTicks);

    public static ShellIconExtractionResult FromBitmapStream(IRandomAccessStream bitmapStream) =>
        new(bitmapStream: bitmapStream);

    // Materialization transfers this bitmap to XAML. Remove it from the extraction
    // result so disposing the remaining extraction data cannot close it mid-copy.
    public SoftwareBitmap? TakeSoftwareBitmap() =>
        Interlocked.Exchange(ref _softwareBitmap, null);

    public void Dispose()
    {
        Interlocked.Exchange(ref _softwareBitmap, null)?.Dispose();
        Interlocked.Exchange(ref _bitmapStream, null)?.Dispose();
    }
}
