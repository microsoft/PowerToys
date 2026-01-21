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
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services.Search;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.Windows.ApplicationModel.Resources;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public static class SearchIndexService
    {
        private static readonly object _lockObject = new();
        private static readonly Dictionary<string, string> _pageNameCache = [];
        private static readonly Dictionary<string, Type> _pageTypeCache = new();
        private static ImmutableArray<SettingEntry> _index = [];
        private static ISearchProvider _searchProvider;
        private static bool _isIndexBuilt;
        private static bool _isIndexBuilding;
        private const string PrebuiltIndexResourceName = "Microsoft.PowerToys.Settings.UI.Assets.search.index.json";
        private static JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Gets or sets the search provider. Defaults to FuzzSearchProvider if not set.
        /// </summary>
        public static ISearchProvider SearchProvider
        {
            get
            {
                lock (_lockObject)
                {
                    _searchProvider ??= new FuzzSearchProvider();
                    return _searchProvider;
                }
            }

            set
            {
                lock (_lockObject)
                {
                    _searchProvider = value;

                    // If index is already built, reinitialize the new provider
                    if (_isIndexBuilt && _searchProvider != null)
                    {
                        _searchProvider.InitializeAsync(_index).ConfigureAwait(false);
                    }
                }
            }
        }

        public static ImmutableArray<SettingEntry> Index
        {
            get
            {
                lock (_lockObject)
                {
                    return _index;
                }
            }
        }

        public static bool IsIndexReady
        {
            get
            {
                lock (_lockObject)
                {
                    return _isIndexBuilt && SearchProvider.IsReady;
                }
            }
        }

        public static void BuildIndex()
        {
            lock (_lockObject)
            {
                if (_isIndexBuilt || _isIndexBuilding)
                {
                    return;
                }

                _isIndexBuilding = true;

                // Clear caches on rebuild
                _pageTypeCache.Clear();
            }

            try
            {
                var builder = ImmutableArray.CreateBuilder<SettingEntry>();
                LoadIndexFromPrebuiltData(builder);

                ImmutableArray<SettingEntry> builtIndex;
                lock (_lockObject)
                {
                    _index = builder.ToImmutable();
                    builtIndex = _index;
                    _isIndexBuilt = true;
                    _isIndexBuilding = false;
                }

                // Initialize the search provider with the index
                SearchProvider.InitializeAsync(builtIndex).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchIndexService] CRITICAL ERROR building search index: {ex.Message}\n{ex.StackTrace}");
                lock (_lockObject)
                {
                    _isIndexBuilding = false;
                    _isIndexBuilt = false;
                }
            }
        }

        private static void LoadIndexFromPrebuiltData(ImmutableArray<SettingEntry>.Builder builder)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            SettingEntry[] metadataList;

            Debug.WriteLine($"[SearchIndexService] Attempting to load prebuilt index from: {PrebuiltIndexResourceName}");

            try
            {
                using Stream stream = assembly.GetManifestResourceStream(PrebuiltIndexResourceName);
                if (stream == null)
                {
                    Debug.WriteLine($"[SearchIndexService] ERROR: Embedded resource '{PrebuiltIndexResourceName}' not found. Ensure it's correctly embedded and the name matches.");
                    return;
                }

                using StreamReader reader = new(stream);
                string json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.WriteLine("[SearchIndexService] ERROR: Embedded resource was empty.");
                    return;
                }

                metadataList = JsonSerializer.Deserialize<SettingEntry[]>(json, _serializerOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchIndexService] ERROR: Failed to load or deserialize prebuilt index: {ex.Message}");
                return;
            }

            if (metadataList == null || metadataList.Length == 0)
            {
                Debug.WriteLine("[SearchIndexService] Prebuilt index is empty or deserialization failed.");
                return;
            }

            foreach (ref var metadata in metadataList.AsSpan())
            {
                if (metadata.Type == EntryType.SettingsPage)
                {
                    (metadata.Header, metadata.Description) = GetLocalizedModuleTitleAndDescription(resourceLoader, metadata.ElementUid);
                }
                else
                {
                    (metadata.Header, metadata.Description) = GetLocalizedSettingHeaderAndDescription(resourceLoader, metadata.ElementUid);
                }

                if (string.IsNullOrEmpty(metadata.Header))
                {
                    continue;
                }

                builder.Add(metadata);

                // Cache the page name mapping for SettingsPage entries
                if (metadata.Type == EntryType.SettingsPage && !string.IsNullOrEmpty(metadata.Header))
                {
                    _pageNameCache[metadata.PageTypeName] = metadata.Header;
                }
            }

            Debug.WriteLine($"[SearchIndexService] Finished loading index. Total entries: {builder.Count}");
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

        public static List<SettingEntry> Search(string query)
        {
            return Search(query, CancellationToken.None);
        }

        public static List<SettingEntry> Search(string query, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            if (!IsIndexReady)
            {
                Debug.WriteLine("[SearchIndexService] Search called but index is not ready.");
                return [];
            }

            // Delegate search to the pluggable search provider
            var results = SearchProvider.Search(query, token);

            // Filter results to ensure page types are valid
            return FilterValidPageTypes(results);
        }

        private static List<SettingEntry> FilterValidPageTypes(List<SettingEntry> results)
        {
            var filtered = new List<SettingEntry>();
            foreach (var entry in results)
            {
                var pageType = GetPageTypeFromName(entry.PageTypeName);
                if (pageType != null)
                {
                    filtered.Add(entry);
                }
            }

            return filtered;
        }

        private static Type GetPageTypeFromName(string pageTypeName)
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

        public static string GetLocalizedPageName(string pageTypeName)
        {
            return _pageNameCache.TryGetValue(pageTypeName, out string cachedName) ? cachedName : string.Empty;
        }
    }
}
