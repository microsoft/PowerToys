// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public sealed partial class ExtensionGalleryService : IExtensionGalleryService
{
    private const string DefaultFeedUrl = "https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/refs/heads/main/extensions.json";
    private const string LocalFeedFileName = "extensions.json";
    private static readonly TimeSpan IconCacheTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan CacheTtl = ExtensionGalleryHttpClient.DefaultTimeToLive;
    private static readonly Action<MEL.ILogger, Exception?> LogGalleryFetchFailedMessage = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(0, nameof(LogGalleryFetchFailed)),
        "Gallery fetch failed");

    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToResolveExtensionGalleryIconMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1, nameof(LogFailedToResolveExtensionGalleryIcon)),
        "Failed to resolve extension gallery icon '{IconUri}'.");

    private readonly ILogger<ExtensionGalleryService> _logger;
    private readonly GalleryFeedUrlProvider _galleryFeedUrlProvider;
    private readonly ExtensionGalleryHttpClient _galleryHttpClient;

    private static readonly HashSet<string> SupportedFeedSchemes =
    [
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeFile,
    ];

    public ExtensionGalleryService(
        ExtensionGalleryHttpClient galleryHttpClient,
        ILogger<ExtensionGalleryService> logger,
        GalleryFeedUrlProvider galleryFeedUrlProvider)
    {
        ArgumentNullException.ThrowIfNull(galleryHttpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(galleryFeedUrlProvider);

        _logger = logger;
        _galleryHttpClient = galleryHttpClient;
        _galleryFeedUrlProvider = galleryFeedUrlProvider;
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
            var cacheableIconUris = CollectCacheableIconUris(extensions);

            if (forceRefresh && !fetchResult.UsedFallbackCache)
            {
                PruneCachedResources(feedUri, cacheableIconUris);
            }

            await LocalizeIconUrisAsync(extensions, cancellationToken);

            return new GalleryFetchResult
            {
                Extensions = extensions,
                FromCache = fetchResult.FromCache,
                UsedFallbackCache = fetchResult.UsedFallbackCache,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException or InvalidOperationException or UriFormatException)
        {
            LogGalleryFetchFailed(_logger, ex);
            var isRateLimited = ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
            return new GalleryFetchResult
            {
                IsRateLimited = isRateLimited,
                HasError = true,
                ErrorMessage = isRateLimited ? null : ex.Message,
            };
        }
    }

    private async Task<FeedFetchResult> FetchFeedDocumentAsync(Uri feedUri, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (feedUri.IsFile)
        {
            var localJson = await File.ReadAllTextAsync(feedUri.LocalPath, cancellationToken);
            return new FeedFetchResult(localJson, FromCache: false, UsedFallbackCache: false);
        }

        if (!feedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !feedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported gallery URI scheme '{feedUri.Scheme}'.");
        }

        var fetchResult = await _galleryHttpClient.Cache.GetResourceAsync(
            feedUri,
            fileNameHint: ResolveFeedFileName(feedUri),
            forceRefresh: forceRefresh,
            timeToLiveOverride: CacheTtl,
            cancellationToken: cancellationToken);
        var responseJson = await File.ReadAllTextAsync(fetchResult.Resource.ContentPath, cancellationToken);
        return new FeedFetchResult(responseJson, fetchResult.Resource.FromCache, fetchResult.UsedFallbackCache);
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
        entry.ScreenshotUrls = NormalizeOptionalUris(entry.ScreenshotUrls, baseDirectoryUri);
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

    private static List<string> NormalizeOptionalUris(List<string>? values, Uri? baseDirectoryUri)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        List<string> normalizedValues = [];
        for (var i = 0; i < values.Count; i++)
        {
            var normalizedValue = NormalizeOptionalUri(values[i], baseDirectoryUri);
            if (normalizedValue is not null)
            {
                normalizedValues.Add(normalizedValue);
            }
        }

        return normalizedValues;
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task LocalizeIconUrisAsync(IEnumerable<GalleryExtensionEntry> extensions, CancellationToken cancellationToken)
    {
        List<Task> localizationTasks = [];
        foreach (var extension in extensions)
        {
            localizationTasks.Add(LocalizeIconUriAsync(extension, cancellationToken));
        }

        await Task.WhenAll(localizationTasks);
    }

    private async Task LocalizeIconUriAsync(GalleryExtensionEntry extension, CancellationToken cancellationToken)
    {
        var iconUrl = ToNullIfWhiteSpace(extension.IconUrl);
        if (iconUrl is null || !Uri.TryCreate(iconUrl, UriKind.Absolute, out var iconUri))
        {
            extension.IconUrl = null;
            return;
        }

        var localizedIconUri = await ResolveLocalizedIconUriAsync(iconUri, cancellationToken);
        extension.IconUrl = localizedIconUri?.AbsoluteUri;
    }

    private async Task<Uri?> ResolveLocalizedIconUriAsync(Uri iconUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(iconUri);

        if (iconUri.IsFile || iconUri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase))
        {
            return iconUri;
        }

        if (!IsCacheableUri(iconUri))
        {
            return null;
        }

        try
        {
            var fetchResult = await _galleryHttpClient.Cache.GetResourceAsync(
                iconUri,
                fileNameHint: Path.GetFileName(Uri.UnescapeDataString(iconUri.AbsolutePath)),
                forceRefresh: false,
                timeToLiveOverride: IconCacheTtl,
                cancellationToken: cancellationToken);

            return fetchResult.Resource.ContentUri;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            LogFailedToResolveExtensionGalleryIcon(_logger, iconUri.AbsoluteUri, ex);
            return null;
        }
    }

    private static List<Uri> CollectCacheableIconUris(IEnumerable<GalleryExtensionEntry> extensions)
    {
        List<Uri> retainedResourceUris = [];
        foreach (var extension in extensions)
        {
            if (!Uri.TryCreate(extension.IconUrl, UriKind.Absolute, out var iconUri)
                || !IsCacheableUri(iconUri))
            {
                continue;
            }

            retainedResourceUris.Add(iconUri);
        }

        return retainedResourceUris;
    }

    private void PruneCachedResources(Uri feedUri, IEnumerable<Uri> cacheableIconUris)
    {
        List<Uri> retainedResourceUris = [];
        if (IsCacheableUri(feedUri))
        {
            retainedResourceUris.Add(feedUri);
        }

        foreach (var iconUri in cacheableIconUris)
        {
            retainedResourceUris.Add(iconUri);
        }

        _galleryHttpClient.Cache.Prune(retainedResourceUris);
    }

    private static bool IsCacheableUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFeedFileName(Uri feedUri)
    {
        var fileNameHint = Path.GetFileName(Uri.UnescapeDataString(feedUri.AbsolutePath));
        return string.IsNullOrWhiteSpace(fileNameHint) ? LocalFeedFileName : fileNameHint;
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

    private static void LogGalleryFetchFailed(MEL.ILogger logger, Exception exception)
    {
        LogGalleryFetchFailedMessage(logger, exception);
    }

    private static void LogFailedToResolveExtensionGalleryIcon(MEL.ILogger logger, string iconUri, Exception exception)
    {
        LogFailedToResolveExtensionGalleryIconMessage(logger, iconUri, exception);
    }

    private sealed record FeedFetchResult(string Json, bool FromCache, bool UsedFallbackCache);
}
