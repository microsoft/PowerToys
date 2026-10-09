// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

/// <summary>
/// Creates a new image source.
/// </summary>
internal static class ImageSourceFactory
{
    private static DispatcherQueue? _dispatcherQueue;

    public static void Initialize()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    internal sealed record ImagePayload(
        IRandomAccessStream Stream,
        string? ContentType,
        Uri? SourceUri);

    internal sealed class ImageDecodeOptions
    {
        public bool SniffContent { get; init; } = true;

        public int? DecodePixelWidth { get; init; }

        public int? DecodePixelHeight { get; init; }
    }

    public static async Task<ImageSource> CreateAsync(ImagePayload payload, ImageDecodeOptions? options = null)
    {
        options ??= new ImageDecodeOptions();

        var isSvg =
            IsSvgByHeaderOrUrl(payload.ContentType, payload.SourceUri) ||
            (options.SniffContent && SniffSvg(payload.Stream));

        payload.Stream.Seek(0);

        return await _dispatcherQueue!.EnqueueAsync(async () =>
        {
            if (isSvg)
            {
                var size = GetSvgSize(payload.Stream.AsStreamForRead());
                payload.Stream.Seek(0);

                var svg = new SvgImageSource();
                await svg.SetSourceAsync(payload.Stream);
                svg.RasterizePixelWidth = size.Width;
                svg.RasterizePixelHeight = size.Height;
                return svg;
            }
            else
            {
                var bmp = new BitmapImage();
                if (options.DecodePixelWidth is int w and > 0)
                {
                    bmp.DecodePixelWidth = w;
                }

                if (options.DecodePixelHeight is int h and > 0)
                {
                    bmp.DecodePixelHeight = h;
                }

                await bmp.SetSourceAsync(payload.Stream);
                return (ImageSource)bmp;
            }
        });
    }

    public static Size GetSvgSize(Stream stream)
    {
        // Parse the SVG string as an XML document
        var svgDocument = XDocument.Load(stream);

        // Get the root element of the document
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        var svgElement = svgDocument.Root;

        // Get the height and width attributes of the root element
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var heightAttribute = svgElement.Attribute("height");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        var widthAttribute = svgElement.Attribute("width");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        // Convert the attribute values to double
        double.TryParse(heightAttribute?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var height);
        double.TryParse(widthAttribute?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var width);

        // Return the height and width as a tuple
        return new(width, height);
    }

    private static bool IsSvgByHeaderOrUrl(string? contentType, Uri? uri)
    {
        if (!string.IsNullOrEmpty(contentType) &&
            contentType.StartsWith("image/svg+xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var s = uri?.ToString();
        return !string.IsNullOrEmpty(s) && s.Contains(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SniffSvg(IRandomAccessStream ras)
    {
        try
        {
            const int maxProbe = 1024;
            ras.Seek(0);
            var s = ras.AsStreamForRead();
            var toRead = (int)Math.Min(ras.Size, maxProbe);
            var buf = new byte[toRead];
            var read = s.Read(buf, 0, toRead);
            if (read <= 0)
            {
                return false;
            }

            var head = System.Text.Encoding.UTF8.GetString(buf, 0, read);
            ras.Seek(0);
            return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            ras.Seek(0);
            return false;
        }
    }
}
