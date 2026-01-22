// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common.Search;
using Common.Search.FuzzSearch;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.Windows.ApplicationModel.Resources;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public sealed class SettingsSearch : IDisposable
    {
        private static readonly Lazy<SettingsSearch> DefaultInstance = new(() => new SettingsSearch());

        private readonly object _lockObject = new();
        private readonly Dictionary<string, string> _pageNameCache = [];
        private readonly Dictionary<string, Type> _pageTypeCache = new();
        private readonly ISearchEngine<SettingEntry> _searchEngine;
        private ImmutableArray<SettingEntry> _index = [];
        private bool _isIndexBuilt;
        private bool _isIndexBuilding;
        private bool _disposed;
        private Task _buildTask;

        private const string PrebuiltIndexResourceName = "Microsoft.PowerToys.Settings.UI.Assets.search.index.json";
        private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

        public SettingsSearch()
            : this(new FuzzSearchEngine<SettingEntry>())
        {
        }

        public SettingsSearch(ISearchEngine<SettingEntry> searchEngine)
        {
            ArgumentNullException.ThrowIfNull(searchEngine);
            _searchEngine = searchEngine;
        }

        public static SettingsSearch Default => DefaultInstance.Value;

        public ImmutableArray<SettingEntry> Index
        {
            get
            {
                lock (_lockObject)
                {
                    return _index;
                }
            }
        }

        public bool IsReady
        {
            get
            {
                lock (_lockObject)
                {
                    return _isIndexBuilt && _searchEngine.IsReady;
                }
            }
        }

        public Task BuildIndexAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                if (_isIndexBuilt)
                {
                    return Task.CompletedTask;
                }

                if (_isIndexBuilding)
                {
                    return _buildTask ?? Task.CompletedTask;
                }

                if (_buildTask != null)
                {
                    return _buildTask;
                }

                _buildTask = BuildIndexInternalAsync(cancellationToken);
                return _buildTask;
            }
        }

        public async Task InitializeIndexAsync(IEnumerable<SettingEntry> entries, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ThrowIfDisposed();

            var builtIndex = entries is ImmutableArray<SettingEntry> immutableEntries
                ? immutableEntries
                : ImmutableArray.CreateRange(entries);

            lock (_lockObject)
            {
                _isIndexBuilding = true;
                _isIndexBuilt = false;
                _index = builtIndex;
                _pageNameCache.Clear();
                _pageTypeCache.Clear();
            }

            CachePageNames(builtIndex);

            try
            {
                if (_searchEngine.IsReady)
                {
                    await _searchEngine.ClearAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _searchEngine.InitializeAsync(cancellationToken).ConfigureAwait(false);
                }

                await _searchEngine.IndexBatchAsync(builtIndex, cancellationToken).ConfigureAwait(false);

                lock (_lockObject)
                {
                    _isIndexBuilt = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSearch] CRITICAL ERROR initializing search engine: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isIndexBuilding = false;
                }
            }
        }

        public async Task<IReadOnlyList<SettingSearchResult>> SearchAsync(
            string query,
            SearchOptions options = null,
            CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<SettingSearchResult>();
            }

            if (!IsReady)
            {
                Debug.WriteLine("[SettingsSearch] Search called but index is not ready.");
                return Array.Empty<SettingSearchResult>();
            }

            var effectiveOptions = options ?? new SearchOptions
            {
                MaxResults = Index.Length,
                IncludeMatchSpans = true,
            };

            try
            {
                var results = await Task.Run(
                    () => _searchEngine.SearchAsync(query, effectiveOptions, token),
                    token).ConfigureAwait(false);
                return FilterValidPageTypes(results);
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<SettingSearchResult>();
            }
        }

        public static IReadOnlyList<SettingEntry> LoadIndexFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<SettingEntry>();
            }

            try
            {
                return JsonSerializer.Deserialize<SettingEntry[]>(json, SerializerOptions) ?? Array.Empty<SettingEntry>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSearch] ERROR: Failed to load index from json: {ex.Message}");
                return Array.Empty<SettingEntry>();
            }
        }

        public string GetLocalizedPageName(string pageTypeName)
        {
            lock (_lockObject)
            {
                return _pageNameCache.TryGetValue(pageTypeName, out string cachedName) ? cachedName : string.Empty;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _searchEngine.Dispose();
            _disposed = true;
        }

        private async Task BuildIndexInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                var entries = LoadIndexFromPrebuiltData();
                await InitializeIndexAsync(entries, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSearch] CRITICAL ERROR building search index: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _buildTask = null;
                }
            }
        }

        private void CachePageNames(ImmutableArray<SettingEntry> entries)
        {
            lock (_lockObject)
            {
                foreach (var entry in entries)
                {
                    if (entry.Type == EntryType.SettingsPage && !string.IsNullOrEmpty(entry.Header))
                    {
                        _pageNameCache[entry.PageTypeName] = entry.Header;
                    }
                }
            }
        }

        private ImmutableArray<SettingEntry> LoadIndexFromPrebuiltData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            Debug.WriteLine($"[SettingsSearch] Attempting to load prebuilt index from: {PrebuiltIndexResourceName}");

            string json;
            try
            {
                using Stream stream = assembly.GetManifestResourceStream(PrebuiltIndexResourceName);
                if (stream == null)
                {
                    Debug.WriteLine($"[SettingsSearch] ERROR: Embedded resource '{PrebuiltIndexResourceName}' not found. Ensure it's correctly embedded and the name matches.");
                    return ImmutableArray<SettingEntry>.Empty;
                }

                using StreamReader reader = new(stream);
                json = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSearch] ERROR: Failed to read prebuilt index: {ex.Message}");
                return ImmutableArray<SettingEntry>.Empty;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("[SettingsSearch] ERROR: Embedded resource was empty.");
                return ImmutableArray<SettingEntry>.Empty;
            }

            var metadataList = LoadIndexFromJson(json);
            if (metadataList == null || metadataList.Count == 0)
            {
                Debug.WriteLine("[SettingsSearch] Prebuilt index is empty or deserialization failed.");
                return ImmutableArray<SettingEntry>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<SettingEntry>(metadataList.Count);

            foreach (var metadata in metadataList)
            {
                var entry = metadata;
                if (entry.Type == EntryType.SettingsPage)
                {
                    (entry.Header, entry.Description) = GetLocalizedModuleTitleAndDescription(resourceLoader, entry.ElementUid);
                }
                else
                {
                    (entry.Header, entry.Description) = GetLocalizedSettingHeaderAndDescription(resourceLoader, entry.ElementUid);
                }

                if (string.IsNullOrEmpty(entry.Header))
                {
                    continue;
                }

                builder.Add(entry);
            }

            Debug.WriteLine($"[SettingsSearch] Finished loading index. Total entries: {builder.Count}");
            return builder.ToImmutable();
        }

        private static (string Header, string Description) GetLocalizedSettingHeaderAndDescription(ResourceLoader resourceLoader, string elementUid)
        {
            string header = GetString(resourceLoader, $"{elementUid}/Header");
            string description = GetString(resourceLoader, $"{elementUid}/Description");

            if (string.IsNullOrEmpty(header))
            {
                header = GetString(resourceLoader, $"{elementUid}/Content");
            }

            return (header, description);
        }

        private static (string Title, string Description) GetLocalizedModuleTitleAndDescription(ResourceLoader resourceLoader, string elementUid)
        {
            string title = GetString(resourceLoader, $"{elementUid}/ModuleTitle");
            string description = GetString(resourceLoader, $"{elementUid}/ModuleDescription");

            return (title, description);
        }

        private static string GetString(ResourceLoader rl, string key)
        {
            try
            {
                string value = rl.GetString(key);
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private IReadOnlyList<SettingSearchResult> FilterValidPageTypes(IReadOnlyList<SearchResult<SettingEntry>> results)
        {
            var filtered = new List<SettingSearchResult>(results.Count);
            foreach (var result in results)
            {
                var entry = result.Item;
                if (GetPageTypeFromName(entry.PageTypeName) != null)
                {
                    filtered.Add(new SettingSearchResult
                    {
                        Entry = entry,
                        Score = result.Score,
                        MatchKind = result.MatchKind,
                        MatchSpans = result.MatchSpans,
                    });
                }
            }

            return filtered;
        }

        private Type GetPageTypeFromName(string pageTypeName)
        {
            if (string.IsNullOrEmpty(pageTypeName))
            {
                return null;
            }

            lock (_lockObject)
            {
                if (_pageTypeCache.TryGetValue(pageTypeName, out var cached))
                {
                    return cached;
                }

                var assembly = typeof(GeneralPage).Assembly;
                var type = assembly.GetType($"Microsoft.PowerToys.Settings.UI.Views.{pageTypeName}");
                _pageTypeCache[pageTypeName] = type;
                return type;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
