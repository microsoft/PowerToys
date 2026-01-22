// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

internal sealed partial class LocalImageSourceProvider : IImageSourceProvider
{
    private readonly ImageSourceFactory.ImageDecodeOptions _decodeOptions;

    public LocalImageSourceProvider(ImageSourceFactory.ImageDecodeOptions? decodeOptions = null)
        => _decodeOptions = decodeOptions ?? new ImageSourceFactory.ImageDecodeOptions();

    public bool ShouldUseThisProvider(string url)
    {
        if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) && uri.IsAbsoluteUri)
        {
            var scheme = uri.Scheme.ToLowerInvariant();
            return scheme is "file" or "ms-appx" or "ms-appdata";
        }

        return false;
    }

    public async Task<ImageSourceInfo> GetImageSource(string url)
    {
        if (!ShouldUseThisProvider(url))
        {
            throw new ArgumentException("Not a local URL/path (file, ms-appx, ms-appdata).", nameof(url));
        }

        // Absolute URI?
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
        {
            var scheme = uri.Scheme.ToLowerInvariant();

            var hints = ImageHints.ParseHintsFromUri(uri);

            if (scheme is "ms-appx" or "ms-appdata")
            {
                // Load directly from the package/appdata URI
                var rasRef = RandomAccessStreamReference.CreateFromUri(uri);
                using var ras = await rasRef.OpenReadAsync();
                var payload = new ImageSourceFactory.ImagePayload(ras, ImageContentTypeHelper.GuessFromPathOrUri(uri.AbsoluteUri), uri);
                return new ImageSourceInfo(await ImageSourceFactory.CreateAsync(payload, _decodeOptions), hints);
            }

            if (scheme is "file")
            {
                var path = uri.LocalPath;
                return new ImageSourceInfo(await FromFilePathAsync(path, uri, _decodeOptions), hints);
            }
        }

        throw new InvalidOperationException("Unsupported local URL/path.");
    }

    private static async Task<ImageSource> FromFilePathAsync(string path, Uri sourceUri, ImageSourceFactory.ImageDecodeOptions decodeOptions)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        using var mem = new InMemoryRandomAccessStream();
        using var outStream = mem.AsStreamForWrite();
        await fs.CopyToAsync(outStream).ConfigureAwait(true);
        await outStream.FlushAsync().ConfigureAwait(true);

        mem.Seek(0);

        var payload = new ImageSourceFactory.ImagePayload(mem, ImageContentTypeHelper.GuessFromPathOrUri(path), sourceUri);
        return await ImageSourceFactory.CreateAsync(payload, decodeOptions).ConfigureAwait(true);
    }

    private static class ImageContentTypeHelper
    {
        public static string? GuessFromPathOrUri(string? pathOrUri)
        {
            if (string.IsNullOrEmpty(pathOrUri))
            {
                return null;
            }

            // Try to get extension from path/uri
            var ext = Path.GetExtension(pathOrUri);
            if (string.IsNullOrEmpty(ext))
            {
                // try query-less URI path portion
                if (Uri.TryCreate(pathOrUri, UriKind.RelativeOrAbsolute, out var u))
                {
                    ext = Path.GetExtension(u.IsAbsoluteUri ? u.AbsolutePath : u.OriginalString);
                }
            }

            ext = ext?.Trim().ToLowerInvariant();
            return ext switch
            {
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".ico" => "image/x-icon",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                ".avif" => "image/avif",
                _ => null,
            };
        }
    }
}
