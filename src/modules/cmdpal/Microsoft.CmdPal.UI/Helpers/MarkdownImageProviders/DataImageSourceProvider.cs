// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

internal sealed partial class DataImageSourceProvider : IImageSourceProvider
{
    private readonly ImageSourceFactory.ImageDecodeOptions _decodeOptions;

    public DataImageSourceProvider(ImageSourceFactory.ImageDecodeOptions? decodeOptions = null)
        => _decodeOptions = decodeOptions ?? new ImageSourceFactory.ImageDecodeOptions();

    public bool ShouldUseThisProvider(string url)
        => url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    public async Task<ImageSourceInfo> GetImageSource(string url)
    {
        if (!ShouldUseThisProvider(url))
        {
            throw new ArgumentException("URL is not a data: URI.", nameof(url));
        }

        // data:[<media type>][;base64],<data>
        var comma = url.IndexOf(',');
        if (comma < 0)
        {
            throw new FormatException("Invalid data URI: missing comma separator.");
        }

        var header = url[5..comma]; // after "data:"
        var payload = url[(comma + 1)..]; // after comma

        // Parse header
        string? contentType = null;
        var isBase64 = false;

        if (!string.IsNullOrEmpty(header))
        {
            var parts = header.Split(';');

            // first token may be media type
            if (!string.IsNullOrWhiteSpace(parts[0]) && parts[0].Contains('/'))
            {
                contentType = parts[0];
            }

            isBase64 = parts.Any(static p => p.Equals("base64", StringComparison.OrdinalIgnoreCase));
        }

        var bytes = isBase64
            ? Convert.FromBase64String(payload)
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));

        var mem = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(mem.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync()!;
        }

        mem.Seek(0);

        var imagePayload = new ImageSourceFactory.ImagePayload(mem, contentType, null);
        var imageSource = await ImageSourceFactory.CreateAsync(imagePayload, _decodeOptions);
        return new ImageSourceInfo(imageSource, new ImageHints
        {
            DownscaleOnly = true,
        });
    }
}
