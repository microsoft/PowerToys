// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

/// <summary>
/// Implementation of IImageProvider to handle http/https images, but adds
/// a new functionality to handle image scaling.
/// </summary>
internal sealed partial class HttpImageSourceProvider : IImageSourceProvider
{
    private readonly HttpClient _http;

    public HttpImageSourceProvider(HttpClient? httpClient = null)
        => _http = httpClient ?? SharedHttpClient.Instance;

    public bool ShouldUseThisProvider(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public async Task<ImageSourceInfo> GetImageSource(string url)
    {
        if (!ShouldUseThisProvider(url))
        {
            throw new ArgumentException("URL must be absolute http/https.", nameof(url));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.TryAddWithoutValidation("Accept", "image/*,text/xml;q=0.9,application/xml;q=0.9,*/*;q=0.8");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var contentType = resp.Content.Headers.ContentType?.MediaType;

        using var mem = new InMemoryRandomAccessStream();
        await CopyToRandomAccessStreamAsync(resp, mem);

        var hints = ImageHints.ParseHintsFromUri(new Uri(url));
        var imageSource = await ImageSourceFactory.CreateAsync(
            new ImageSourceFactory.ImagePayload(mem, contentType, new Uri(url)),
            new ImageSourceFactory.ImageDecodeOptions { SniffContent = true });

        return new ImageSourceInfo(imageSource, hints);
    }

    private static async Task CopyToRandomAccessStreamAsync(HttpResponseMessage resp, InMemoryRandomAccessStream mem)
    {
        var data = await resp.Content.ReadAsByteArrayAsync();
        await mem.WriteAsync(data.AsBuffer());
        mem.Seek(0);
    }

    private static class SharedHttpClient
    {
        public static readonly HttpClient Instance = Create();

        private static HttpClient Create()
        {
            var c = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("CommandPalette/1.0");
            return c;
        }
    }
}
