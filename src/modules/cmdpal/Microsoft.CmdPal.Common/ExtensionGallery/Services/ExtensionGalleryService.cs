// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.Services;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public sealed partial class ExtensionGalleryService : IExtensionGalleryService, IDisposable
{
    private const string DefaultFeedUrl = "https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/refs/heads/main/extensions.json";
    private const string LocalFeedFileName = "extensions.json";
    private const string CacheDirectoryName = "GalleryCache";
    private const int TimeoutSeconds = 15;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);
    private static readonly TimeSpan IconCacheTtl = TimeSpan.FromDays(1);

    private readonly HttpClient _httpClient;
    private readonly Func<string?> _galleryFeedUrlProvider;
    private readonly string _cacheDirectory;
    private readonly HttpResourceCache _resourceCache;
    private static readonly HashSet<string> SupportedFeedSchemes =
    [
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeFile,
    ];

    public ExtensionGalleryService(IApplicationInfoService applicationInfoService, Func<string?>? galleryFeedUrlProvider = null)
        : this(galleryFeedUrlProvider, applicationInfoService, cacheDirectory: null, httpClient: null)
    {
        ArgumentNullException.ThrowIfNull(applicationInfoService);
    }

    internal ExtensionGalleryService(Func<string?>? galleryFeedUrlProvider, IApplicationInfoService applicationInfoService, string? cacheDirectory, HttpClient? httpClient)
    {
        _galleryFeedUrlProvider = galleryFeedUrlProvider ?? (() => null);
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys-CmdPal/1.0");

        _cacheDirectory = cacheDirectory ?? Path.Combine(applicationInfoService.CacheDirectory, CacheDirectoryName);
        Directory.CreateDirectory(_cacheDirectory);
        _resourceCache = new HttpResourceCache(_httpClient, _cacheDirectory, CacheTtl);
    }

    public bool IsCustomFeed => !string.IsNullOrWhiteSpace(_galleryFeedUrlProvider());

    public string GetBaseUrl()
    {
        return GetFeedUrl();
    }

    public string GetFeedUrl()
    {
        var configuredUrl = _galleryFeedUrlProvider();
        return string.IsNullOrWhiteSpace(configuredUrl) ? DefaultFeedUrl : configuredUrl.Trim();
    }

    public async Task<Uri?> GetCachedIconUriAsync(Uri iconUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(iconUri);

        if (iconUri.IsFile || iconUri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase))
        {
            return iconUri;
        }

        if (!iconUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !iconUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fileNameHint = Path.GetFileName(Uri.UnescapeDataString(iconUri.AbsolutePath));
        var cachedResource = await _resourceCache.GetOrFetchAsync(
            iconUri,
            fileNameHint,
            forceRefresh: false,
            timeToLiveOverride: IconCacheTtl,
            cancellationToken: cancellationToken);
        return cachedResource?.ContentUri;
    }

    public Task<GalleryFetchResult> FetchExtensionsAsync(CancellationToken cancellationToken = default)
    {
        return FetchWrappedFeedAsync(forceRefresh: false, cancellationToken);
    }

    public Task<GalleryFetchResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        return FetchWrappedFeedAsync(forceRefresh: true, cancellationToken);
    }

    private async Task<GalleryFetchResult> FetchWrappedFeedAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetFeedUri(out var feedUri))
            {
                throw new InvalidOperationException($"Invalid gallery feed URL '{GetFeedUrl()}'.");
            }

            var fetchResult = await FetchFeedDocumentAsync(feedUri, forceRefresh, cancellationToken);
            var extensions = TryParseWrappedGallery(fetchResult.Json);
            if (extensions is null || extensions.Count == 0)
            {
                throw new InvalidOperationException("The extension gallery feed is empty or invalid.");
            }

            TryGetBaseDirectoryUri(feedUri, out var baseDirectoryUri);
            NormalizeRemoteEntries(extensions, baseDirectoryUri);

            if (forceRefresh && !fetchResult.UsedFallbackCache)
            {
                PruneCachedResources(feedUri, extensions);
            }

            return new GalleryFetchResult
            {
                Extensions = extensions,
                FromCache = fetchResult.FromCache,
                UsedFallbackCache = fetchResult.UsedFallbackCache,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException or InvalidOperationException or UriFormatException)
        {
            CoreLogger.LogError("Gallery fetch failed", ex);
            return new GalleryFetchResult
            {
                HasError = true,
                ErrorMessage = ex.Message,
            };
        }
    }

    private async Task<FeedFetchResult> FetchFeedDocumentAsync(Uri feedUri, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (feedUri.IsFile)
        {
            var json = await File.ReadAllTextAsync(feedUri.LocalPath, cancellationToken);
            return new FeedFetchResult(json, FromCache: false, UsedFallbackCache: false);
        }

        if (!feedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !feedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported gallery URI scheme '{feedUri.Scheme}'.");
        }

        var fileNameHint = Path.GetFileName(Uri.UnescapeDataString(feedUri.AbsolutePath));
        var cachedFeed = await _resourceCache.GetOrFetchWithStatusAsync(
            resourceUri: feedUri,
            fileNameHint: string.IsNullOrWhiteSpace(fileNameHint) ? LocalFeedFileName : fileNameHint,
            forceRefresh: forceRefresh,
            timeToLiveOverride: CacheTtl,
            cancellationToken: cancellationToken);
        if (cachedFeed?.Resource is null)
        {
            throw new HttpRequestException("Could not reach an extension gallery.");
        }

        var cachedJson = await File.ReadAllTextAsync(cachedFeed.Resource.ContentPath, cancellationToken);
        return new FeedFetchResult(cachedJson, cachedFeed.Resource.FromCache, cachedFeed.UsedFallbackCache);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static List<GalleryExtensionEntry>? TryParseWrappedGallery(string json)
    {
        try
        {
            var index = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryRemoteIndex);
            return index?.Extensions;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void NormalizeRemoteEntries(List<GalleryExtensionEntry> entries, Uri? baseDirectoryUri)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.Id = entry.Id.Trim();
            NormalizeEntry(entry, baseDirectoryUri);
        }
    }

    private static void NormalizeEntry(GalleryExtensionEntry entry, Uri? baseDirectoryUri)
    {
        entry.IconUrl = NormalizeOptionalUri(entry.IconUrl, baseDirectoryUri);
    }

    private static string? NormalizeOptionalUri(string? value, Uri? baseDirectoryUri)
    {
        var normalizedValue = ToNullIfWhiteSpace(value);
        if (normalizedValue is null)
        {
            return null;
        }

        if (Uri.TryCreate(normalizedValue, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        if (baseDirectoryUri is null || !Uri.TryCreate(baseDirectoryUri, normalizedValue, out var candidate))
        {
            return normalizedValue;
        }

        if (!candidate.AbsoluteUri.StartsWith(baseDirectoryUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedValue;
        }

        return candidate.AbsoluteUri;
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private void PruneCachedResources(Uri feedUri, IEnumerable<GalleryExtensionEntry> extensions)
    {
        List<Uri> retainedResourceUris = [];
        if (IsCacheableUri(feedUri))
        {
            retainedResourceUris.Add(feedUri);
        }

        foreach (var extension in extensions)
        {
            if (!Uri.TryCreate(extension.IconUrl, UriKind.Absolute, out var iconUri)
                || !IsCacheableUri(iconUri))
            {
                continue;
            }

            retainedResourceUris.Add(iconUri);
        }

        _resourceCache.Prune(retainedResourceUris);
    }

    private static bool IsCacheableUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetFeedUri([NotNullWhen(true)] out Uri? feedUri)
    {
        feedUri = null;
        var feedUrl = GetFeedUrl();
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (!SupportedFeedSchemes.Contains(candidate.Scheme))
        {
            return false;
        }

        if (candidate.IsFile && Directory.Exists(candidate.LocalPath))
        {
            candidate = new Uri(Path.Combine(candidate.LocalPath, LocalFeedFileName));
        }

        feedUri = candidate;
        return true;
    }

    private static bool TryGetBaseDirectoryUri(Uri feedUri, [NotNullWhen(true)] out Uri? baseDirectoryUri)
    {
        baseDirectoryUri = null;
        try
        {
            var candidate = new Uri(feedUri, ".");
            if (!SupportedFeedSchemes.Contains(candidate.Scheme))
            {
                return false;
            }

            baseDirectoryUri = candidate;
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private sealed record FeedFetchResult(string Json, bool FromCache, bool UsedFallbackCache);
}
