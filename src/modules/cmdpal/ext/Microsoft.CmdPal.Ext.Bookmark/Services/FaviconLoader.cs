// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services.HttpCaching;
using Microsoft.Extensions.Logging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public sealed partial class FaviconLoader : IFaviconLoader, IDisposable
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsCommandPalette/1.0";
    private static readonly TimeSpan CacheTimeToLive = TimeSpan.FromDays(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly Action<ILogger, Exception?> LogCacheUnavailable = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(0, nameof(LogCacheUnavailable)),
        "The favicon disk cache is unavailable. Using direct requests for this session.");

    private static readonly Action<ILogger, Exception?> LogCacheRequestFailed = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(1, nameof(LogCacheRequestFailed)),
        "Could not use the favicon disk cache for this request. Retrying directly.");

    private readonly Lazy<HttpCachingClient?> _cache;
    private readonly Lazy<HttpClient> _direct;
    private readonly ILogger<FaviconLoader> _logger;
    private bool _disposed;

    public FaviconLoader(string cacheDirectory, ILogger<FaviconLoader> logger)
        : this(cacheDirectory, handlerFactory: null, logger)
    {
    }

    internal FaviconLoader(string cacheDirectory, Func<HttpMessageHandler>? handlerFactory, ILogger<FaviconLoader> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        handlerFactory ??= CreateHandler;
        _cache = new Lazy<HttpCachingClient?>(() => CreateCache(cacheDirectory, handlerFactory));
        _direct = new Lazy<HttpClient>(() => CreateDirectClient(handlerFactory));
    }

    private static HttpMessageHandler CreateHandler() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
    };

    private HttpCachingClient? CreateCache(string cacheDirectory, Func<HttpMessageHandler> handlerFactory)
    {
        HttpMessageHandler? handler = null;
        try
        {
            handler = handlerFactory();
            return new HttpCachingClient(cacheDirectory, CacheTimeToLive, RequestTimeout, UserAgent, handler, _logger, accept: "image/*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            handler?.Dispose();
            LogCacheUnavailable(_logger, ex);
            return null;
        }
    }

    private static HttpClient CreateDirectClient(Func<HttpMessageHandler> handlerFactory)
    {
        var client = new HttpClient(handlerFactory()) { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*");
        return client;
    }

    public async Task<IRandomAccessStream?> TryGetFaviconAsync(Uri siteUri, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (siteUri.Scheme != Uri.UriSchemeHttp && siteUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var first = BuildFaviconUri(siteUri);
        var result = await TryFetchAsync(first, ct).ConfigureAwait(false);
        if (result.Stream is not null)
        {
            return result.Stream;
        }

        if (result.FinalUri is not { } finalUri || UriEqualsAuthority(first, finalUri))
        {
            return null;
        }

        var secondUri = BuildFaviconUri(finalUri);
        if (result.FinalUriAlreadyChecked && secondUri == finalUri)
        {
            return null;
        }

        var second = await TryFetchAsync(secondUri, ct).ConfigureAwait(false);
        return second.Stream;
    }

    public void Prune(IEnumerable<Uri> retainedSiteUris)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(retainedSiteUris);

        if (_cache.Value is not { } cache)
        {
            return;
        }

        var retainedFaviconUris = new HashSet<Uri>();
        foreach (var uri in retainedSiteUris)
        {
            if (uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                retainedFaviconUris.Add(BuildFaviconUri(uri));
            }
        }

        cache.Prune(retainedFaviconUris, getRedirectResourceUri: BuildFaviconUri);
    }

    private static Uri BuildFaviconUri(Uri anyUriOnSite)
    {
        var builder = new UriBuilder(anyUriOnSite.Scheme, anyUriOnSite.Host)
        {
            Port = anyUriOnSite.IsDefaultPort ? -1 : anyUriOnSite.Port,
            Path = "/favicon.ico",
        };
        return builder.Uri;
    }

    private async Task<(IRandomAccessStream? Stream, Uri? FinalUri, bool FinalUriAlreadyChecked)> TryFetchAsync(Uri url, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (_cache.Value is { } cache)
            {
                try
                {
                    // shortcut: HTML bodies retain redirect links, use metadata-only records if their disk cost matters
                    var result = await cache.GetResourceAsync(url, minimumTimeToLive: CacheTimeToLive, reuseFailedRedirects: true, cancellationToken: ct).ConfigureAwait(false);
                    var resource = result.Resource;
                    var stream = IsImage(resource.ContentType)
                        ? await CreateStreamAsync(await File.ReadAllBytesAsync(resource.ContentPath, ct).ConfigureAwait(false), ct).ConfigureAwait(false)
                        : null;
                    return (stream, resource.FinalUri, true);
                }
                catch (HttpResourceRequestException ex)
                {
                    return (null, ex.FinalUri, ex.FinalUriAlreadyChecked);
                }
                catch (Exception ex) when (ex is HttpResourceCacheStoreException or IOException or UnauthorizedAccessException)
                {
                    // shortcut: persistent disk failures retry per lookup, add root health tracking if the extra requests become costly
                    LogCacheRequestFailed(_logger, ex);
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _direct.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var finalUri = response.RequestMessage?.RequestUri;
            if (!response.IsSuccessStatusCode || !IsImage(response.Content.Headers.ContentType?.MediaType))
            {
                return (null, finalUri, true);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return (await CreateStreamAsync(bytes, ct).ConfigureAwait(false), finalUri, true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // shortcut: cache timeouts are misses, add stale-on-timeout support if offline latency becomes a problem
            return default;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return default;
        }
    }

    private static bool IsImage(string? mediaType)
    {
        return mediaType is null || mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IRandomAccessStream> CreateStreamAsync(byte[] bytes, CancellationToken ct)
    {
        var stream = new InMemoryRandomAccessStream();
        try
        {
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
        catch
        {
            stream.Dispose();
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

        if (_cache.IsValueCreated)
        {
            _cache.Value?.Dispose();
        }

        if (_direct.IsValueCreated)
        {
            _direct.Value.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
