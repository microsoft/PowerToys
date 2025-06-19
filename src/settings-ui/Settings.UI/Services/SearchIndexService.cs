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
        SettingsCard,
        SettingsExpander,
    }

#pragma warning disable SA1649 // File name should match first type name
    public readonly struct SettingEntry
#pragma warning restore SA1649 // File name should match first type name
    {
        public readonly EntryType Type;
        public readonly string Module;
        public readonly string Header;
        public readonly string PageTypeName;
        public readonly string ElementName;
        public readonly string ElementUid;
        public readonly string ParentElementName;
        public readonly string Description;

        public SettingEntry(EntryType type, string module, string header, string pageTypeName, string elementName, string elementUid, string parentElementName = null, string description = null)
        {
            Type = type;
            Module = module;
            Header = header;
            PageTypeName = pageTypeName;
            ElementName = elementName;
            ElementUid = elementUid;
            ParentElementName = parentElementName;
            Description = description;
        }
    }

    public readonly struct SearchHit
    {
        public readonly string Module;
        public readonly string Caption;
        public readonly string Description;
        public readonly double Score;
        public readonly Type PageType;
        public readonly string ElementName;
        public readonly string ElementUid;
        public readonly string ParentElementName;

        public SearchHit(string module, string caption, string description, double score, Type pageType, string elementName, string elementUid, string parentElementName = null)
        {
            Module = module;
            Caption = caption;
            Description = description;
            Score = score;
            PageType = pageType;
            ElementName = elementName;
            ElementUid = elementUid;
            ParentElementName = parentElementName;
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal sealed class SearchableElementMetadata
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string PageName { get; set; }

        public EntryType Type { get; set; }

        public string ParentElementName { get; set; }

        public string ElementName { get; set; }

        public string ElementUid { get; set; }
    }

    public static class SearchIndexService
    {
        private static readonly object _lockObject = new();
        private static ImmutableArray<SettingEntry> _index = ImmutableArray<SettingEntry>.Empty;
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

            foreach (var metadata in metadataList.OrderBy(m => m.PageName))
            {
                string moduleName = GetModuleNameFromTypeName(metadata.PageName);

                var (header, description) = GetLocalizedHeaderAndDescription(resourceLoader, metadata.ElementUid);

                if (string.IsNullOrEmpty(header))
                {
                    Debug.WriteLine($"[SearchIndexService] WARNING: Missing primary localization for ElementUid: '{metadata.ElementUid}' in Page: '{metadata.PageName}'. Skipping.");
                    continue;
                }

                builder.Add(new SettingEntry(
                    metadata.Type,
                    moduleName,
                    header,
                    metadata.PageName,
                    metadata.ElementName,
                    metadata.ElementUid,
                    metadata.ParentElementName,
                    description));

                Debug.WriteLine($"[SearchIndexService] ADDED: [{metadata.Type}] {moduleName} - {header} (ElementName: {metadata.ElementName}, ElementUid: {metadata.ElementUid}, Page: {metadata.PageName}, Parent: {metadata.ParentElementName ?? "None"})");
            }

            Debug.WriteLine($"[SearchIndexService] Finished loading index. Total entries: {builder.Count}");
        }

        private static (string Header, string Description) GetLocalizedHeaderAndDescription(ResourceLoader resourceLoader, string elementUid)
        {
            string header = GetString(resourceLoader, $"{elementUid}/Header");
            string description = GetString(resourceLoader, $"{elementUid}/Description");

            if (string.IsNullOrEmpty(header))
            {
                Debug.WriteLine($"[SearchIndexService] WARNING: No header localization found for ElementUid: '{elementUid}'");
            }

            return (header, description);
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

        private static string GetModuleNameFromTypeName(string typeName) => typeName switch
        {
            "GeneralPage" => "General",
            "DashboardPage" => "Dashboard",
            "AdvancedPastePage" => "Advanced Paste",
            "AlwaysOnTopPage" => "Always On Top",
            "AwakePage" => "Awake",
            "CmdPalPage" => "Command Palette",
            "ColorPickerPage" => "Color Picker",
            "CropAndLockPage" => "Crop And Lock",
            "EnvironmentVariablesPage" => "Environment Variables",
            "FancyZonesPage" => "FancyZones",
            "FileLocksmithPage" => "File Locksmith",
            "HostsPage" => "Hosts File Editor",
            "ImageResizerPage" => "Image Resizer",
            "KeyboardManagerPage" => "Keyboard Manager",
            "MeasureToolPage" => "Screen Ruler",
            "MouseUtilsPage" => "Mouse Utilities",
            "MouseWithoutBordersPage" => "Mouse Without Borders",
            "NewPlusPage" => "New+",
            "PeekPage" => "Peek",
            "PowerAccentPage" => "Quick Accent",
            "PowerLauncherPage" => "PowerToys Run",
            "PowerOcrPage" => "Text Extractor",
            "PowerPreviewPage" => "File Explorer Add-ons",
            "PowerRenamePage" => "PowerRename",
            "RegistryPreviewPage" => "Registry Preview",
            "ShortcutGuidePage" => "Shortcut Guide",
            "WorkspacesPage" => "Workspaces",
            "ZoomItPage" => "ZoomIt",
            _ when typeName.EndsWith("Page", StringComparison.InvariantCultureIgnoreCase) => typeName.Substring(0, typeName.Length - 4),
            _ => typeName,
        };

        public static List<SearchHit> Search(string query, int maxResults = 20)
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
            var results = new List<(SearchHit Hit, double Score)>();

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
                        var hit = new SearchHit(
                            entry.Module,
                            entry.Header,
                            entry.Description,
                            score,
                            pageType,
                            entry.ElementName,
                            entry.ElementUid,
                            entry.ParentElementName);
                        results.Add((hit, score));
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
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
    }
}
