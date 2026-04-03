// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public sealed partial class ExtensionGalleryService : IExtensionGalleryService, IDisposable
{
    private const string DefaultFeedUrl = "https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/refs/heads/main/extensions.json";
    private const string CachedIndexFileName = "index.json";
    private const string ManifestFileName = "manifest.json";
    private const string ManifestLocalizedPrefix = "manifest.";
    private const string CacheDirectoryName = "GalleryCache";
    private const string CacheTimestampFileName = ".last_fetch";
    private const int TimeoutSeconds = 15;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    private readonly HttpClient _httpClient;
    private readonly Func<string?> _galleryFeedUrlProvider;
    private readonly string _cacheDirectory;
    private static readonly HashSet<string> SupportedFeedSchemes =
    [
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeFile,
    ];

    public ExtensionGalleryService(Func<string?>? galleryFeedUrlProvider = null)
        : this(galleryFeedUrlProvider, cacheDirectory: null, httpClient: null)
    {
    }

    internal ExtensionGalleryService(Func<string?>? galleryFeedUrlProvider, string? cacheDirectory, HttpClient? httpClient)
    {
        _galleryFeedUrlProvider = galleryFeedUrlProvider ?? (() => null);
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys-CmdPal/1.0");

        var baseDir = cacheDirectory ?? Path.Combine(Utilities.BaseSettingsPath("Microsoft.CmdPal"), CacheDirectoryName);
        _cacheDirectory = baseDir;
        Directory.CreateDirectory(_cacheDirectory);
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

    public string? GetIconUrl(string extensionId, string iconFilename)
    {
        if (string.IsNullOrWhiteSpace(extensionId) || string.IsNullOrWhiteSpace(iconFilename))
        {
            return null;
        }

        var safeExtensionId = Uri.EscapeDataString(extensionId);
        var safeIconFilename = Uri.EscapeDataString(iconFilename);
        return TryBuildUri($"extensions/{safeExtensionId}/{safeIconFilename}", out var iconUri) ? iconUri.AbsoluteUri : null;
    }

    public async Task<GalleryFetchResult> FetchExtensionsAsync(CancellationToken cancellationToken = default)
    {
        if (IsCacheFresh())
        {
            var cached = TryLoadFromCache(errorMessage: null);
            if (!cached.HasError && cached.Extensions.Count > 0)
            {
                cached.FromCache = true;
                return cached;
            }
        }

        return await FetchRemoteAsync(cancellationToken);
    }

    public async Task<GalleryFetchResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Always bypass the TTL check and fetch fresh data
        return await FetchRemoteAsync(cancellationToken);
    }

    private async Task<GalleryFetchResult> FetchRemoteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetFeedUri(out var feedUri))
            {
                throw new InvalidOperationException($"Invalid gallery feed URL '{GetFeedUrl()}'.");
            }

            var json = await FetchStringAsync(feedUri, cancellationToken);

            // Try wrapped gallery format (full extension data inline).
            var inlineExtensions = TryParseWrappedGallery(json);
            if (inlineExtensions is not null && inlineExtensions.Count > 0)
            {
                NormalizeRemoteEntries(inlineExtensions);
                var indexEntries = BuildIndexFromExtensions(inlineExtensions);
                CacheResults(indexEntries, inlineExtensions);
                TouchCacheTimestamp();
                return new GalleryFetchResult { Extensions = inlineExtensions };
            }

            // Fall back to index + per-manifest fetch.
            var index = ParseIndex(json);
            if (index == null || index.Count == 0)
            {
                return TryLoadFromCache("Empty or null index received.");
            }

            var manifests = await FetchManifestsAsync(index, cancellationToken);
            CacheResults(index, manifests);
            TouchCacheTimestamp();

            return new GalleryFetchResult { Extensions = manifests };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException or InvalidOperationException or UriFormatException)
        {
            CoreLogger.LogError("Gallery fetch failed", ex);
            return TryLoadFromCache(ex.Message);
        }
    }

    private async Task<List<GalleryIndexEntry>?> FetchIndexAsync(CancellationToken cancellationToken)
    {
        if (!TryGetFeedUri(out var feedUri))
        {
            throw new InvalidOperationException($"Invalid gallery feed URL '{GetFeedUrl()}'.");
        }

        var json = await FetchStringAsync(feedUri, cancellationToken);
        return ParseIndex(json);
    }

    private async Task<List<GalleryExtensionEntry>> FetchManifestsAsync(
        List<GalleryIndexEntry> indexEntries,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<GalleryExtensionEntry?>>(indexEntries.Count);
        for (var i = 0; i < indexEntries.Count; i++)
        {
            tasks.Add(FetchManifestAsync(indexEntries[i], cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        var manifests = new List<GalleryExtensionEntry>(results.Length);
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is not null)
            {
                manifests.Add(results[i]!);
            }
        }

        return manifests;
    }

    private async Task<GalleryExtensionEntry?> FetchManifestAsync(GalleryIndexEntry indexEntry, CancellationToken cancellationToken)
    {
        var extensionId = indexEntry.Id;
        try
        {
            var safeExtensionId = Uri.EscapeDataString(extensionId);
            if (!TryBuildUri($"extensions/{safeExtensionId}/{ManifestFileName}", out var manifestUri))
            {
                return null;
            }

            var json = await FetchStringAsync(manifestUri, cancellationToken);
            var manifest = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryExtensionEntry);
            if (manifest is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                manifest.Id = extensionId;
            }

            var localizedManifest = await TryFetchLocalizedManifestAsync(safeExtensionId, extensionId, cancellationToken);
            if (localizedManifest is not null)
            {
                manifest = MergeLocalizedManifest(manifest, localizedManifest);
            }

            manifest.Tags = MergeTags(indexEntry.Tags, manifest.Tags);
            return manifest;
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"Failed to fetch manifest for '{extensionId}'.", ex);
            return null;
        }
    }

    private async Task<string> FetchStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsFile)
        {
            return await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);
        }

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            return await _httpClient.GetStringAsync(uri, cancellationToken);
        }

        throw new InvalidOperationException($"Unsupported gallery URI scheme '{uri.Scheme}'.");
    }

    private async Task<GalleryExtensionEntry?> TryFetchLocalizedManifestAsync(
        string safeExtensionId,
        string extensionId,
        CancellationToken cancellationToken)
    {
        var locales = GetPreferredManifestLocales();
        for (var i = 0; i < locales.Count; i++)
        {
            var manifestFileName = $"{ManifestLocalizedPrefix}{locales[i]}.json";
            if (!TryBuildUri($"extensions/{safeExtensionId}/{manifestFileName}", out var manifestUri))
            {
                continue;
            }

            var json = await TryFetchStringAsync(manifestUri, cancellationToken);
            if (json is null)
            {
                continue;
            }

            GalleryExtensionEntry? localized;
            try
            {
                localized = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryExtensionEntry);
            }
            catch (JsonException ex)
            {
                CoreLogger.LogError($"Ignoring invalid localized manifest for '{extensionId}' ({manifestFileName}).", ex);
                continue;
            }

            if (localized is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(localized.Id))
            {
                localized.Id = extensionId;
            }

            return localized;
        }

        return null;
    }

    private async Task<string?> TryFetchStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchStringAsync(uri, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> GetPreferredManifestLocales()
    {
        List<string> locales = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        var culture = CultureInfo.CurrentUICulture;
        while (!culture.Equals(CultureInfo.InvariantCulture))
        {
            var normalizedCulture = ToNullIfWhiteSpace(culture.Name)?.ToLowerInvariant();
            if (normalizedCulture is not null && seen.Add(normalizedCulture))
            {
                locales.Add(normalizedCulture);
            }

            culture = culture.Parent;
        }

        return locales;
    }

    private static GalleryExtensionEntry MergeLocalizedManifest(GalleryExtensionEntry baseManifest, GalleryExtensionEntry localizedManifest)
    {
        return new GalleryExtensionEntry
        {
            Id = !string.IsNullOrWhiteSpace(baseManifest.Id) ? baseManifest.Id : localizedManifest.Id,
            Title = !string.IsNullOrWhiteSpace(localizedManifest.Title) ? localizedManifest.Title : baseManifest.Title,
            Description = !string.IsNullOrWhiteSpace(localizedManifest.Description) ? localizedManifest.Description : baseManifest.Description,
            Author = MergeAuthor(baseManifest.Author, localizedManifest.Author),
            Homepage = !string.IsNullOrWhiteSpace(localizedManifest.Homepage) ? localizedManifest.Homepage : baseManifest.Homepage,
            Readme = !string.IsNullOrWhiteSpace(localizedManifest.Readme) ? localizedManifest.Readme : baseManifest.Readme,
            Icon = !string.IsNullOrWhiteSpace(localizedManifest.Icon) ? localizedManifest.Icon : baseManifest.Icon,
            IconDark = !string.IsNullOrWhiteSpace(localizedManifest.IconDark) ? localizedManifest.IconDark : baseManifest.IconDark,
            InstallSources = localizedManifest.InstallSources.Count > 0 ? localizedManifest.InstallSources : baseManifest.InstallSources,
            Detection = localizedManifest.Detection ?? baseManifest.Detection,
            Tags = MergeTags(baseManifest.Tags, localizedManifest.Tags),
        };
    }

    private static GalleryAuthor MergeAuthor(GalleryAuthor baseAuthor, GalleryAuthor localizedAuthor)
    {
        return new GalleryAuthor
        {
            Name = !string.IsNullOrWhiteSpace(localizedAuthor.Name) ? localizedAuthor.Name : baseAuthor.Name,
            Url = !string.IsNullOrWhiteSpace(localizedAuthor.Url) ? localizedAuthor.Url : baseAuthor.Url,
        };
    }

    private void CacheResults(List<GalleryIndexEntry> index, List<GalleryExtensionEntry> manifests)
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, CachedIndexFileName);
            var indexJson = SerializeIndex(index);
            File.WriteAllText(indexPath, indexJson);

            foreach (var manifest in manifests)
            {
                var manifestDir = Path.Combine(_cacheDirectory, manifest.Id);
                Directory.CreateDirectory(manifestDir);
                var manifestPath = Path.Combine(manifestDir, ManifestFileName);
                var manifestJson = JsonSerializer.Serialize(manifest, GallerySerializationContext.Default.GalleryExtensionEntry);
                File.WriteAllText(manifestPath, manifestJson);
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to cache gallery data.", ex);
        }
    }

    private GalleryFetchResult TryLoadFromCache(string? errorMessage)
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, CachedIndexFileName);
            if (!File.Exists(indexPath))
            {
                return new GalleryFetchResult
                {
                    HasError = true,
                    ErrorMessage = errorMessage,
                };
            }

            var indexJson = File.ReadAllText(indexPath);
            var index = ParseIndex(indexJson);
            if (index == null || index.Count == 0)
            {
                return new GalleryFetchResult
                {
                    HasError = true,
                    ErrorMessage = errorMessage,
                };
            }

            var manifests = new List<GalleryExtensionEntry>();
            foreach (var indexEntry in index)
            {
                var manifestPath = Path.Combine(_cacheDirectory, indexEntry.Id, ManifestFileName);
                if (File.Exists(manifestPath))
                {
                    var manifestJson = File.ReadAllText(manifestPath);
                    var entry = JsonSerializer.Deserialize(manifestJson, GallerySerializationContext.Default.GalleryExtensionEntry);
                    if (entry != null)
                    {
                        entry.Tags = MergeTags(indexEntry.Tags, entry.Tags);
                        manifests.Add(entry);
                    }
                }
            }

            return new GalleryFetchResult
            {
                Extensions = manifests,
                FromCache = true,
            };
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to load cached gallery data.", ex);
            return new GalleryFetchResult
            {
                HasError = true,
                ErrorMessage = errorMessage,
            };
        }
    }

    private bool IsCacheFresh()
    {
        try
        {
            var timestampPath = Path.Combine(_cacheDirectory, CacheTimestampFileName);
            if (!File.Exists(timestampPath))
            {
                return false;
            }

            var lastWrite = File.GetLastWriteTimeUtc(timestampPath);
            return DateTime.UtcNow - lastWrite < CacheTtl;
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to check gallery cache freshness", ex);
            return false;
        }
    }

    private void TouchCacheTimestamp()
    {
        try
        {
            var timestampPath = Path.Combine(_cacheDirectory, CacheTimestampFileName);
            File.WriteAllText(timestampPath, string.Empty);
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to update gallery cache timestamp", ex);
        }
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

    private static void NormalizeRemoteEntries(List<GalleryExtensionEntry> entries)
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
        }
    }

    private static List<GalleryIndexEntry> BuildIndexFromExtensions(IReadOnlyList<GalleryExtensionEntry> extensions)
    {
        var index = new List<GalleryIndexEntry>(extensions.Count);
        for (var i = 0; i < extensions.Count; i++)
        {
            index.Add(new GalleryIndexEntry
            {
                Id = extensions[i].Id,
                Tags = extensions[i].Tags,
            });
        }

        return index;
    }

    private static List<GalleryIndexEntry>? ParseIndex(string json)
    {
        List<GalleryIndexEntry>? modernEntries = null;
        try
        {
            modernEntries = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryIndexEntries);
        }
        catch (JsonException)
        {
            // Fallback to legacy string id list below.
        }

        if (modernEntries is not null && modernEntries.Count > 0)
        {
            return NormalizeIndexEntries(modernEntries);
        }

        List<string>? legacyIds = null;
        try
        {
            legacyIds = JsonSerializer.Deserialize(json, GallerySerializationContext.Default.GalleryIndexIds);
        }
        catch (JsonException)
        {
            return null;
        }

        if (legacyIds is null || legacyIds.Count == 0)
        {
            return null;
        }

        var legacyEntries = new List<GalleryIndexEntry>(legacyIds.Count);
        for (var i = 0; i < legacyIds.Count; i++)
        {
            var normalizedId = ToNullIfWhiteSpace(legacyIds[i]);
            if (normalizedId is null)
            {
                continue;
            }

            legacyEntries.Add(new GalleryIndexEntry { Id = normalizedId });
        }

        return legacyEntries;
    }

    private static List<GalleryIndexEntry> NormalizeIndexEntries(IReadOnlyList<GalleryIndexEntry> entries)
    {
        var normalized = new List<GalleryIndexEntry>(entries.Count);
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < entries.Count; i++)
        {
            var normalizedId = ToNullIfWhiteSpace(entries[i].Id);
            if (normalizedId is null || !seenIds.Add(normalizedId))
            {
                continue;
            }

            normalized.Add(new GalleryIndexEntry
            {
                Id = normalizedId,
                Tags = NormalizeTags(entries[i].Tags),
            });
        }

        return normalized;
    }

    private static string SerializeIndex(IReadOnlyList<GalleryIndexEntry> indexEntries)
    {
        var hasAnyTags = false;
        for (var i = 0; i < indexEntries.Count; i++)
        {
            if (indexEntries[i].Tags.Count > 0)
            {
                hasAnyTags = true;
                break;
            }
        }

        if (!hasAnyTags)
        {
            List<string> legacyIds = new(indexEntries.Count);
            for (var i = 0; i < indexEntries.Count; i++)
            {
                legacyIds.Add(indexEntries[i].Id);
            }

            return JsonSerializer.Serialize(legacyIds, GallerySerializationContext.Default.GalleryIndexIds);
        }

        return JsonSerializer.Serialize(indexEntries, GallerySerializationContext.Default.GalleryIndexEntries);
    }

    private static List<string> MergeTags(IReadOnlyList<string>? indexTags, IReadOnlyList<string>? manifestTags)
    {
        List<string> merged = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        AddNormalizedTags(merged, seen, manifestTags);
        AddNormalizedTags(merged, seen, indexTags);
        return merged;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        AddNormalizedTags(normalized, seen, tags);
        return normalized;
    }

    private static void AddNormalizedTags(ICollection<string> target, ISet<string> seen, IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return;
        }

        for (var i = 0; i < tags.Count; i++)
        {
            var normalizedTag = ToNullIfWhiteSpace(tags[i]);
            if (normalizedTag is null || !seen.Add(normalizedTag))
            {
                continue;
            }

            target.Add(normalizedTag);
        }
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

        feedUri = candidate;
        return true;
    }

    private bool TryGetBaseDirectoryUri([NotNullWhen(true)] out Uri? baseDirectoryUri)
    {
        baseDirectoryUri = null;
        if (!TryGetFeedUri(out var feedUri))
        {
            return false;
        }

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

    private bool TryBuildUri(string relativePath, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!TryGetBaseDirectoryUri(out var baseDirectoryUri))
        {
            return false;
        }

        if (!Uri.TryCreate(baseDirectoryUri, relativePath, out var candidate))
        {
            return false;
        }

        // Prevent path traversal: resolved URI must stay within the feed base directory.
        if (!candidate.AbsoluteUri.StartsWith(baseDirectoryUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }
}
