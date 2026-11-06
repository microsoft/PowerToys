// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public sealed partial class FaviconLoader : IFaviconLoader, IDisposable
{
    private readonly HttpClient _http = CreateClient();
    private bool _disposed;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        var client = new HttpClient(handler, disposeHandler: true);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsCommandPalette/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*");

        return client;
    }

    public async Task<IRandomAccessStream?> TryGetFaviconAsync(Uri siteUri, CancellationToken ct = default)
    {
        if (siteUri.Scheme != Uri.UriSchemeHttp && siteUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // 1) First attempt: favicon on the original authority (preserves port).
        var first = BuildFaviconUri(siteUri);

        // Check if the image already exists in the cache
        ApplicationInfoService applicationInfoService = new();

        var cacheDirectory = Path.Combine(applicationInfoService.CacheDirectory, "Microsoft.CmdPal", "favicons");
        Directory.CreateDirectory(cacheDirectory); // Create the cache directory if it does not already exist

        // Hash the hostname, and generate the file path for it
        var cacheItem = SHA256.HashData(Encoding.UTF8.GetBytes(first.Host));
        var cacheItemPath = Path.Combine(cacheDirectory, $"{Convert.ToHexString(cacheItem)}.ico");
        var cacheExpiry = DateTime.Now.AddDays(-1);

        var cacheItemExists = File.Exists(cacheItemPath);
        var cacheItemStale = true;

        if (cacheItemExists)
        {
            // See if the cache item is less than a day old
            cacheItemStale = File.GetLastWriteTime(cacheItemPath) < cacheExpiry;
        }

        if (cacheItemExists && !cacheItemStale)
        {
            return await TryOpenFileAsync(cacheItemPath, ct);
        }

        // Try download; if this fails (non-image or path lost), retry on final host.
        var stream = await TryDownloadImageAsync(first, ct).ConfigureAwait(false);
        if (stream is not null)
        {
            // Try write the downloaded image out to the cache
            await TryWriteFileAsync(cacheItemPath, stream, ct);
            return stream;
        }

        // 2) If the server redirected and "lost" the path, try /favicon.ico on the *final* host.
        // We discover the final host by doing a HEAD/GET to the original URL and inspecting the final RequestUri.
        var finalAuthority = await ResolveFinalAuthorityAsync(first, ct).ConfigureAwait(false);
        if (finalAuthority is null || UriEqualsAuthority(first, finalAuthority))
        {
            // Try to return the cached favicon instead, if one is present
            if (cacheItemExists)
            {
                return await TryOpenFileAsync(cacheItemPath, ct);
            }

            return null;
        }

        var second = BuildFaviconUri(finalAuthority);
        if (second == first)
        {
            // Try to return the cached favicon instead, if one is present
            if (cacheItemExists)
            {
                return await TryOpenFileAsync(cacheItemPath, ct);
            }

            return null; // nothing new to try
        }

        cacheItem = SHA256.HashData(Encoding.UTF8.GetBytes(second.Host));
        cacheItemPath = Path.Combine(cacheDirectory, $"{Convert.ToHexString(cacheItem)}.ico");

        cacheItemExists = File.Exists(cacheItemPath);
        cacheItemStale = true;

        if (cacheItemExists)
        {
            // See if the cache item is less than a day old
            cacheItemStale = File.GetLastWriteTime(cacheItemPath) < cacheExpiry;
        }

        if (cacheItemExists && !cacheItemStale)
        {
            return await TryOpenFileAsync(cacheItemPath, ct);
        }

        stream = await TryDownloadImageAsync(second, ct).ConfigureAwait(false);
        if (stream is not null)
        {
            // Try write the downloaded image out to the cache
            await TryWriteFileAsync(cacheItemPath, stream, ct);
        }

        return stream;
    }

    private static Uri BuildFaviconUri(Uri anyUriOnSite)
    {
        var b = new UriBuilder(anyUriOnSite.Scheme, anyUriOnSite.Host)
        {
            Port = anyUriOnSite.IsDefaultPort ? -1 : anyUriOnSite.Port,
            Path = "/favicon.ico",
        };
        return b.Uri;
    }

    private async Task<Uri?> ResolveFinalAuthorityAsync(Uri url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        // We only need headers to learn the final RequestUri after redirects
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                                    .ConfigureAwait(false);

        var final = resp.RequestMessage?.RequestUri;
        return final is null ? null : new UriBuilder(final.Scheme, final.Host)
        {
            Port = final.IsDefaultPort ? -1 : final.Port,
            Path = "/",
        }.Uri;
    }

    private async Task<IRandomAccessStream?> TryDownloadImageAsync(Uri url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            // If the redirect chain dumped us on an HTML page (common for root), bail.
            var mediaType = resp.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null &&
                !mediaType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var stream = new InMemoryRandomAccessStream();

            using (var output = stream.GetOutputStreamAt(0))
            using (var writer = new DataWriter(output))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(ct);
                await writer.FlushAsync().AsTask(ct);
            }

            stream.Seek(0);
            return stream;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IRandomAccessStream?> TryOpenFileAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, ct);
            var stream = new InMemoryRandomAccessStream();

            using (var output = stream.GetOutputStreamAt(0))
            using (var writer = new DataWriter(output))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(ct);
                await writer.FlushAsync().AsTask(ct);
            }

            stream.Seek(0);
            return stream;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task TryWriteFileAsync(string path, IRandomAccessStream stream, CancellationToken ct)
    {
        try
        {
            stream.Seek(0);
            using (var reader = new DataReader(stream))
            {
                await reader.LoadAsync((uint)stream.Size);
                var buffer = new byte[(uint)stream.Size];
                reader.ReadBytes(buffer);
                await File.WriteAllBytesAsync(path, buffer, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static bool UriEqualsAuthority(Uri a, Uri b)
        => a.Scheme.Equals(b.Scheme, StringComparison.OrdinalIgnoreCase)
        && a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase)
        && (a.IsDefaultPort ? -1 : a.Port) == (b.IsDefaultPort ? -1 : b.Port);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _http.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
