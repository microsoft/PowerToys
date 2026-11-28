// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Tracks decode sizing for a stream-backed image preview.
/// </summary>
internal sealed class ImagePreviewDecode
{
    private Size _nativeSize = Size.Empty;
    private Size _fittedSize;
    private bool? _loadSucceeded;

    /// <summary>Gets the requested preview bounds in DIPs.</summary>
    public Size Bounds { get; }

    /// <summary>Initializes a new instance of the <see cref="ImagePreviewDecode"/> class.</summary>
    /// <param name="bounds">Preview bounds in DIPs, or <see cref="Size.Empty"/> to defer decoding.</param>
    public ImagePreviewDecode(Size bounds)
    {
        Bounds = bounds;
    }

    /// <summary>Combines layout, content, and viewport limits in DIPs.</summary>
    /// <param name="availableSize">The size offered by layout.</param>
    /// <param name="maxSize">The image's display limits.</param>
    /// <param name="viewportSize">The viewport size.</param>
    /// <returns>The usable bounds, or <see cref="Size.Empty"/> if either dimension is unavailable.</returns>
    public static Size GetBounds(Size availableSize, Size maxSize, Size viewportSize)
    {
        var width = Math.Min(maxSize.Width, Math.Min(availableSize.Width, viewportSize.Width));
        var height = Math.Min(maxSize.Height, Math.Min(availableSize.Height, viewportSize.Height));
        return double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0
            ? new Size(width, height)
            : Size.Empty;
    }

    /// <summary>Records the outcome of the current request.</summary>
    /// <param name="loadSucceeded">Whether the preview bitmap loaded.</param>
    public void Complete(bool loadSucceeded)
    {
        _loadSucceeded = loadSucceeded;
    }

    /// <summary>Determines whether the new bounds warrant another decode.</summary>
    /// <param name="newBounds">The new preview bounds in DIPs.</param>
    /// <param name="scale">Physical pixels per DIP.</param>
    public bool NeedsRefresh(Size newBounds, double scale)
    {
        if (newBounds.IsEmpty || _loadSucceeded is not { } loadSucceeded)
        {
            return false;
        }

        if (Bounds.IsEmpty)
        {
            return true;
        }

        var pixelBounds = new Size(newBounds.Width * scale, newBounds.Height * scale);
        if (!loadSucceeded || _nativeSize.IsEmpty)
        {
            return Math.Floor(pixelBounds.Width) > Math.Floor(Bounds.Width * scale)
                || Math.Floor(pixelBounds.Height) > Math.Floor(Bounds.Height * scale);
        }

        var target = FitToBounds(_nativeSize, pixelBounds);
        return target.Width > _fittedSize.Width || target.Height > _fittedSize.Height;
    }

    /// <summary>Reads the native image dimensions and rewinds the stream.</summary>
    /// <param name="stream">The encoded image stream.</param>
    /// <param name="pixelBounds">The preview bounds in physical pixels.</param>
    /// <returns>The fitted decode size, or <see cref="Size.Empty"/> if the native size fits.</returns>
    public async Task<Size> GetSizeAsync(IRandomAccessStream stream, Size pixelBounds)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
        _nativeSize = new Size(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
        stream.Seek(0);

        _fittedSize = FitToBounds(_nativeSize, pixelBounds);
        return _fittedSize.Equals(_nativeSize) ? Size.Empty : _fittedSize;
    }

    private static Size FitToBounds(Size nativeSize, Size pixelBounds)
    {
        var maxWidth = Math.Max(1, Math.Floor(pixelBounds.Width));
        var maxHeight = Math.Max(1, Math.Floor(pixelBounds.Height));
        var scale = Math.Min(1, Math.Min(maxWidth / nativeSize.Width, maxHeight / nativeSize.Height));
        return new Size(
            Math.Max(1, Math.Round(nativeSize.Width * scale)),
            Math.Max(1, Math.Round(nativeSize.Height * scale)));
    }
}
