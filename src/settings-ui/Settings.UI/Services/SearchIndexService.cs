// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public enum EntryType
    {
        SettingsPage,
        SettingsCard,
        SettingsExpander,
    }

#pragma warning disable SA1649 // File name should match first type name
    public readonly struct SettingEntry
#pragma warning restore SA1649 // File name should match first type name
    {
        public readonly EntryType Type;
        public readonly string Header;
        public readonly string PageTypeName;
        public readonly string ElementName;
        public readonly string ElementUid;
        public readonly string ParentElementName;
        public readonly string Description;
        public readonly string Icon;

        public SettingEntry(EntryType type, string header, string pageTypeName, string elementName, string elementUid, string parentElementName = null, string description = null, string icon = null)
        {
            Type = type;
            Header = header;
            PageTypeName = pageTypeName;
            ElementName = elementName;
            ElementUid = elementUid;
            ParentElementName = parentElementName;
            Description = description;
            Icon = icon;
        }
    }

    public static class SearchIndexService
    {
        private static readonly object _lockObject = new();
        private static readonly Dictionary<string, string> _pageNameCache = [];
        private static ImmutableArray<SettingEntry> _index = [];
        private static bool _isIndexBuilt;
        private static bool _isIndexBuilding;
        private const string PrebuiltIndexResourceName = "Microsoft.PowerToys.Settings.UI.Assets.search.index.json";
        private static JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

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
                    return _isIndexBuilt;
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
            }

            try
            {
                var builder = ImmutableArray.CreateBuilder<SettingEntry>();
                LoadIndexFromPrebuiltData(builder);

                lock (_lockObject)
                {
                    _index = builder.ToImmutable();
                    _isIndexBuilt = true;
                    _isIndexBuilding = false;
                }
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
            List<SearchableElementMetadata> metadataList = null;

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

                metadataList = JsonSerializer.Deserialize<List<SearchableElementMetadata>>(json, _serializerOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchIndexService] ERROR: Failed to load or deserialize prebuilt index: {ex.Message}");
                return;
            }

            if (metadataList == null || metadataList.Count == 0)
            {
                Debug.WriteLine("[SearchIndexService] Prebuilt index is empty or deserialization failed.");
                return;
            }

            foreach (var metadata in metadataList)
            {
                string header, description;

                if (metadata.Type == EntryType.SettingsPage)
                {
                    (header, description) = GetLocalizedModuleTitleAndDescription(resourceLoader, metadata.ElementUid);
                }
                else
                {
                    (header, description) = GetLocalizedSettingHeaderAndDescription(resourceLoader, metadata.ElementUid);
                }

                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                builder.Add(new SettingEntry(
                    metadata.Type,
                    header, // header
                    metadata.PageName, // pageTypeName
                    metadata.ElementName,
                    metadata.ElementUid,
                    metadata.ParentElementName,
                    description,
                    metadata.Icon));

                // Cache the page name mapping for SettingsPage entries
                if (metadata.Type == EntryType.SettingsPage && !string.IsNullOrEmpty(header))
                {
                    _pageNameCache[metadata.PageName] = header;
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
                Debug.WriteLine($"[SearchIndexService] WARNING: No header localization found for ElementUid: '{elementUid}'");
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
            var results = new List<(SettingEntry Hit, double Score)>();

            foreach (var entry in currentIndex)
            {
                var captionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, NormalizeString(entry.Header));
                double score = captionScoreResult.Score;

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    var descriptionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, NormalizeString(entry.Description));
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
                        results.Add((entry, score));
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Select(r => r.Hit)
                .ToList();
        }

        private static Type GetPageTypeFromName(string pageTypeName)
        {
            var assembly = typeof(GeneralPage).Assembly;
            return assembly.GetType($"Microsoft.PowerToys.Settings.UI.Views.{pageTypeName}");
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

#pragma warning disable SA1402 // File may only contain a single type
    public class SearchableElementMetadata
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string PageName { get; set; }

        public EntryType Type { get; set; }

        public string ParentElementName { get; set; }

        public string ElementName { get; set; }

        public string ElementUid { get; set; }

        public string Icon { get; set; }
    }
}
