// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common.Search.FuzzSearch;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.Windows.ApplicationModel.Resources;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public static class SearchIndexService
    {
        // Use ReaderWriterLockSlim for better concurrent read performance
        private static readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.SupportsRecursion);
        private static readonly Dictionary<string, string> _pageNameCache = [];
        private static readonly Dictionary<string, (string HeaderNorm, string DescNorm)> _normalizedTextCache = new();
        private static readonly Dictionary<string, Type> _pageTypeCache = new();
        private static ImmutableArray<SettingEntry> _index = [];
        private static bool _isIndexBuilt;
        private static bool _isIndexBuilding;
        private const string PrebuiltIndexResourceName = "Microsoft.PowerToys.Settings.UI.Assets.search.index.json";
        private static JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

        public static ImmutableArray<SettingEntry> Index
        {
            get
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _index;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        public static bool IsIndexReady
        {
            get
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _isIndexBuilt;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Builds the search index asynchronously on a background thread.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public static Task BuildIndexAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => BuildIndex(), cancellationToken);
        }

        public static void BuildIndex()
        {
            _rwLock.EnterWriteLock();
            try
            {
                if (_isIndexBuilt || _isIndexBuilding)
                {
                    return;
                }

                _isIndexBuilding = true;

                // Clear caches on rebuild
                _normalizedTextCache.Clear();
                _pageTypeCache.Clear();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            try
            {
                var builder = ImmutableArray.CreateBuilder<SettingEntry>();
                LoadIndexFromPrebuiltData(builder);

                _rwLock.EnterWriteLock();
                try
                {
                    _index = builder.ToImmutable();
                    _isIndexBuilt = true;
                    _isIndexBuilding = false;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchIndexService] CRITICAL ERROR building search index: {ex.Message}\n{ex.StackTrace}");
                _rwLock.EnterWriteLock();
                try
                {
                    _isIndexBuilding = false;
                    _isIndexBuilt = false;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
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

            var currentIndex = Index;
            if (currentIndex.IsEmpty)
            {
                Debug.WriteLine("[SearchIndexService] Search called but index is empty.");
                return [];
            }

            var normalizedQuery = NormalizeString(query);
            var bag = new ConcurrentBag<(SettingEntry Hit, double Score)>();
            var po = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
            };

            try
            {
                Parallel.ForEach(currentIndex, po, entry =>
                {
                    var (headerNorm, descNorm) = GetNormalizedTexts(entry);
                    var captionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, headerNorm);
                    double score = captionScoreResult.Score;

                    if (!string.IsNullOrEmpty(descNorm))
                    {
                        var descriptionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, descNorm);
                        if (descriptionScoreResult.Success)
                        {
                            score = Math.Max(score, descriptionScoreResult.Score * 0.8);
                        }
                    }

                    if (score > 0)
                    {
                        var pageType = GetPageTypeFromName(entry.PageTypeName);
                        if (pageType != null)
                        {
                            bag.Add((entry, score));
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return [];
            }

            return bag
                .OrderByDescending(r => r.Score)
                .Select(r => r.Hit)
                .ToList();
        }

        private static Type GetPageTypeFromName(string pageTypeName)
        {
            if (string.IsNullOrEmpty(pageTypeName))
            {
                return null;
            }

            // Try read lock first for cache lookup
            _rwLock.EnterReadLock();
            try
            {
                if (_pageTypeCache.TryGetValue(pageTypeName, out var cached))
                {
                    return cached;
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            // Cache miss - need write lock to add
            _rwLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_pageTypeCache.TryGetValue(pageTypeName, out var cached))
                {
                    return cached;
                }

                var assembly = typeof(GeneralPage).Assembly;
                var type = assembly.GetType($"Microsoft.PowerToys.Settings.UI.Views.{pageTypeName}");
                _pageTypeCache[pageTypeName] = type;
                return type;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private static (string HeaderNorm, string DescNorm) GetNormalizedTexts(SettingEntry entry)
        {
            if (entry.ElementUid == null && entry.Header == null)
            {
                return (NormalizeString(entry.Header), NormalizeString(entry.Description));
            }

            var key = entry.ElementUid ?? $"{entry.PageTypeName}|{entry.ElementName}";

            // Try read lock first for cache lookup
            _rwLock.EnterReadLock();
            try
            {
                if (_normalizedTextCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            // Cache miss - compute values and add to cache
            var headerNorm = NormalizeString(entry.Header);
            var descNorm = NormalizeString(entry.Description);

            _rwLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (!_normalizedTextCache.ContainsKey(key))
                {
                    _normalizedTextCache[key] = (headerNorm, descNorm);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return (headerNorm, descNorm);
        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var normalized = input.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }

        public static string GetLocalizedPageName(string pageTypeName)
        {
            return _pageNameCache.TryGetValue(pageTypeName, out string cachedName) ? cachedName : string.Empty;
        }
    }
}
