// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

public sealed partial class HttpCachingClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IHttpResourceCacheStore _cacheStore;
    private readonly HttpResourceCacheHandler _cacheHandler;

    public HttpCachingClient(
        string cacheDirectory,
        TimeSpan defaultTimeToLive,
        TimeSpan timeout,
        string? userAgent,
        HttpMessageHandler? innerHandler,
        MEL.ILogger logger,
        string? accept = null)
        : this(
            new FileSystemHttpResourceCacheStore(cacheDirectory, defaultTimeToLive, logger),
            timeout,
            userAgent,
            innerHandler,
            logger,
            accept)
    {
    }

    internal HttpCachingClient(
        IHttpResourceCacheStore cacheStore,
        TimeSpan timeout,
        string? userAgent,
        HttpMessageHandler? innerHandler,
        MEL.ILogger logger,
        string? accept = null)
    {
        ArgumentNullException.ThrowIfNull(cacheStore);
        ArgumentNullException.ThrowIfNull(logger);

        _cacheStore = cacheStore;
        _cacheHandler = new HttpResourceCacheHandler(cacheStore, innerHandler ?? new HttpClientHandler(), logger);
        _httpClient = new HttpClient(_cacheHandler) { Timeout = timeout };

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        if (!string.IsNullOrWhiteSpace(accept))
        {
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd(accept);
        }
    }

    public async Task<CachedHttpFetchResult> GetResourceAsync(
        Uri resourceUri,
        string? fileNameHint = null,
        bool forceRefresh = false,
        TimeSpan? timeToLiveOverride = null,
        TimeSpan? minimumTimeToLive = null,
        bool reuseFailedRedirects = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);

        if (!IsSupportedHttpUri(resourceUri))
        {
            throw new InvalidOperationException($"Unsupported HTTP resource URI scheme '{resourceUri.Scheme}'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, resourceUri);
        HttpResourceCacheHandler.ConfigureRequest(
            request,
            fileNameHint: fileNameHint,
            forceRefresh: forceRefresh,
            timeToLiveOverride: timeToLiveOverride,
            minimumTimeToLive: minimumTimeToLive,
            reuseFailedRedirects: reuseFailedRedirects);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var cacheInfo = HttpResourceCacheHandler.GetResponseInfo(response);
        if (cacheInfo.Resource is null)
        {
            throw new InvalidOperationException($"The HTTP cache did not produce a cached resource for '{resourceUri}'.");
        }

        return new CachedHttpFetchResult(cacheInfo.Resource, cacheInfo.UsedFallbackCache);
    }

    /// <summary>Removes cached resources that are no longer needed.</summary>
    /// <remarks>
    /// The optional selector maps saved redirect destinations to additional resource URIs to retain, without making network requests.
    /// </remarks>
    public void Prune(IEnumerable<Uri> retainedResourceUris, Func<Uri, Uri>? getRedirectResourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(retainedResourceUris);

        List<Uri> retainedUris = [.. retainedResourceUris];
        _cacheHandler.AddInflightResourceUris(retainedUris);
        if (getRedirectResourceUri is not null)
        {
            foreach (var resourceUri in retainedUris.ToArray())
            {
                if (!resourceUri.IsAbsoluteUri || !IsSupportedHttpUri(resourceUri))
                {
                    continue;
                }

                var metadata = _cacheStore.GetEntry(resourceUri).Metadata;
                if (Uri.TryCreate(metadata?.FinalUri, UriKind.Absolute, out var finalUri) && IsSupportedHttpUri(finalUri))
                {
                    retainedUris.Add(getRedirectResourceUri(finalUri));
                }
            }
        }

        _cacheStore.Prune(retainedUris);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _cacheHandler.Dispose();
    }

    private static bool IsSupportedHttpUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
