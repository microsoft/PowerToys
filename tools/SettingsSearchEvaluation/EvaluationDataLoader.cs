// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SettingsSearchEvaluation;

internal static partial class EvaluationDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static (IReadOnlyList<SettingEntry> Entries, DatasetDiagnostics Diagnostics) LoadEntriesFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        var resources = TryLoadResourceMap(path);
        return LoadEntriesFromJson(json, resources);
    }

    public static (IReadOnlyList<SettingEntry> Entries, DatasetDiagnostics Diagnostics) LoadEntriesFromJson(string json)
    {
        return LoadEntriesFromJson(json, resourceMap: null);
    }

    public static (IReadOnlyList<SettingEntry> Entries, DatasetDiagnostics Diagnostics) LoadEntriesFromJson(
        string json,
        IReadOnlyDictionary<string, string>? resourceMap)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (Array.Empty<SettingEntry>(), DatasetDiagnostics.Empty);
        }

        var rawEntries = JsonSerializer.Deserialize<List<RawSettingEntry>>(json, JsonOptions) ?? new List<RawSettingEntry>();
        var normalized = new List<SettingEntry>(rawEntries.Count);

        foreach (var raw in rawEntries)
        {
            var pageType = raw.PageTypeName?.Trim() ?? string.Empty;
            var elementName = raw.ElementName?.Trim() ?? string.Empty;
            var elementUid = raw.ElementUid?.Trim() ?? string.Empty;
            var parent = raw.ParentElementName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(elementUid))
            {
                elementUid = $"{pageType}|{elementName}";
            }

            var localized = ResolveLocalizedStrings(raw.Type, elementUid, elementName, parent, resourceMap);

            var header = raw.Header?.Trim();
            if (string.IsNullOrEmpty(header))
            {
                header = localized.Header;
            }

            if (string.IsNullOrEmpty(header))
            {
                header = BuildFallbackHeader(elementUid, elementName, pageType);
            }

            var description = raw.Description?.Trim();
            if (string.IsNullOrEmpty(description))
            {
                description = localized.Description;
            }

            description ??= string.Empty;
            var icon = raw.Icon?.Trim() ?? string.Empty;

            normalized.Add(new SettingEntry(
                raw.Type,
                header,
                pageType,
                elementName,
                elementUid,
                parent,
                description,
                icon));
        }

        return (normalized, BuildDiagnostics(normalized));
    }

    public static (IReadOnlyList<SettingEntry> Entries, DatasetDiagnostics Diagnostics) LoadEntriesFromNormalizedCorpusFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadEntriesFromNormalizedCorpusLines(File.ReadLines(path));
    }

    public static (IReadOnlyList<SettingEntry> Entries, DatasetDiagnostics Diagnostics) LoadEntriesFromNormalizedCorpusLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<SettingEntry>();
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                continue;
            }

            var splitIndex = line.IndexOf('\t');
            string id;
            string normalizedText;
            if (splitIndex <= 0)
            {
                id = $"line:{lineNumber}";
                normalizedText = line;
            }
            else
            {
                id = line[..splitIndex].Trim();
                normalizedText = line[(splitIndex + 1)..].Trim();

                if (string.IsNullOrWhiteSpace(id))
                {
                    id = $"line:{lineNumber}";
                }
            }

            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                continue;
            }

            entries.Add(new SettingEntry(
                EntryType.SettingsCard,
                normalizedText,
                string.Empty,
                string.Empty,
                id,
                string.Empty,
                string.Empty,
                string.Empty));
        }

        return (entries, BuildDiagnostics(entries));
    }

    public static void WriteNormalizedCorpusFile(string path, IEnumerable<SettingEntry> entries)
    {
        WriteNormalizedCorpusFile(path, entries, includeId: true);
    }

    public static void WriteNormalizedTextCorpusFile(string path, IEnumerable<SettingEntry> entries)
    {
        WriteNormalizedCorpusFile(path, entries, includeId: false);
    }

    private static void WriteNormalizedCorpusFile(string path, IEnumerable<SettingEntry> entries, bool includeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(entries);

        var outputDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var entry in entries)
        {
            var normalizedText = BuildNormalizedSearchText(entry);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                continue;
            }

            if (includeId)
            {
                var id = SanitizeCorpusId(entry.Id);
                writer.Write(id);
                writer.Write('\t');
            }

            writer.WriteLine(normalizedText);
        }
    }

    public static IReadOnlyList<EvaluationCase> LoadCases(string? casesPath, IReadOnlyList<SettingEntry> entries)
    {
        if (!string.IsNullOrWhiteSpace(casesPath))
        {
            var json = File.ReadAllText(casesPath);
            var parsed = JsonSerializer.Deserialize<List<RawEvaluationCase>>(json, JsonOptions) ?? new List<RawEvaluationCase>();
            var normalized = parsed
                .Where(c => !string.IsNullOrWhiteSpace(c.Query))
                .Select(c => new EvaluationCase
                {
                    Query = c.Query!.Trim(),
                    ExpectedIds = c.ExpectedIds?
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray() ?? Array.Empty<string>(),
                    Notes = c.Notes,
                })
                .Where(c => c.ExpectedIds.Count > 0)
                .ToList();

            if (normalized.Count > 0)
            {
                return normalized;
            }
        }

        return GenerateFallbackCases(entries);
    }

    private static DatasetDiagnostics BuildDiagnostics(IReadOnlyList<SettingEntry> entries)
    {
        var duplicateBuckets = entries
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count())
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new DatasetDiagnostics
        {
            TotalEntries = entries.Count,
            DistinctIds = entries.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            DuplicateIdBucketCount = duplicateBuckets.Count,
            DuplicateIdCounts = new ReadOnlyDictionary<string, int>(duplicateBuckets),
        };
    }

    private static IReadOnlyList<EvaluationCase> GenerateFallbackCases(IReadOnlyList<SettingEntry> entries)
    {
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Header) && !string.IsNullOrWhiteSpace(entry.Id))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(40)
            .Select(entry => new EvaluationCase
            {
                Query = entry.Header,
                ExpectedIds = new[] { entry.Id },
                Notes = "Autogenerated case from index entry header.",
            })
            .ToArray();
    }

    private static string BuildNormalizedSearchText(SettingEntry entry)
    {
        var combined = string.IsNullOrWhiteSpace(entry.SecondarySearchableText)
            ? entry.SearchableText
            : $"{entry.SearchableText} {entry.SecondarySearchableText}";

        return NormalizeForCorpus(combined);
    }

    private static string NormalizeForCorpus(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch);
        }

        var noMarks = builder.ToString();
        var compact = ConsecutiveWhitespaceRegex().Replace(noMarks, " ").Trim();
        return compact.Replace('\t', ' ');
    }

    private static string SanitizeCorpusId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "unknown";
        }

        return id.Trim().Replace('\t', ' ');
    }

    private static string BuildFallbackHeader(string elementUid, string elementName, string pageTypeName)
    {
        var candidate = !string.IsNullOrWhiteSpace(elementUid)
            ? elementUid
            : (!string.IsNullOrWhiteSpace(elementName) ? elementName : pageTypeName);

        candidate = candidate.Replace('_', ' ').Trim();
        candidate = ConsecutiveWhitespaceRegex().Replace(candidate, " ");
        candidate = CamelBoundaryRegex().Replace(candidate, "$1 $2");
        return candidate;
    }

    private static (string Header, string Description) ResolveLocalizedStrings(
        EntryType type,
        string elementUid,
        string elementName,
        string parentElementName,
        IReadOnlyDictionary<string, string>? resourceMap)
    {
        if (resourceMap == null || string.IsNullOrWhiteSpace(elementUid))
        {
            return (string.Empty, string.Empty);
        }

        var uid = elementUid.Trim();
        var header = string.Empty;
        var description = string.Empty;

        if (type == EntryType.SettingsPage)
        {
            header = GetFirstResourceString(
                resourceMap,
                $"{uid}.ModuleTitle",
                $"{uid}/ModuleTitle",
                $"{uid}.Title",
                $"{uid}/Title");

            description = GetFirstResourceString(
                resourceMap,
                $"{uid}.ModuleDescription",
                $"{uid}/ModuleDescription",
                $"{uid}.Description",
                $"{uid}/Description");
        }
        else
        {
            header = GetFirstResourceString(
                resourceMap,
                $"{uid}.Header",
                $"{uid}/Header",
                $"{uid}.Content",
                $"{uid}/Content",
                $"{uid}.Title",
                $"{uid}/Title",
                $"{uid}.Text",
                $"{uid}/Text",
                $"{uid}.PlaceholderText",
                $"{uid}/PlaceholderText");

            description = GetFirstResourceString(
                resourceMap,
                $"{uid}.Description",
                $"{uid}/Description",
                $"{uid}.Message",
                $"{uid}/Message",
                $"{uid}.Text",
                $"{uid}/Text");
        }

        if (string.IsNullOrWhiteSpace(header))
        {
            header = TryResolveByTokenAndSuffixes(
                resourceMap,
                uid,
                "header",
                "title",
                "content",
                "text",
                "placeholdertext");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = TryResolveByTokenAndSuffixes(
                resourceMap,
                uid,
                "description",
                "message",
                "subtitle",
                "text");
        }

        if (string.IsNullOrWhiteSpace(header) && !string.IsNullOrWhiteSpace(parentElementName))
        {
            header = TryResolveByTokenAndSuffixes(
                resourceMap,
                parentElementName,
                "header",
                "title",
                "content",
                "text",
                "placeholdertext");
        }

        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(parentElementName))
        {
            description = TryResolveByTokenAndSuffixes(
                resourceMap,
                parentElementName,
                "description",
                "message",
                "subtitle",
                "text");
        }

        if (string.IsNullOrWhiteSpace(header) && !string.IsNullOrWhiteSpace(elementName))
        {
            header = TryResolveByTokenAndSuffixes(
                resourceMap,
                elementName,
                "header",
                "title",
                "content",
                "text",
                "placeholdertext");
        }

        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(elementName))
        {
            description = TryResolveByTokenAndSuffixes(
                resourceMap,
                elementName,
                "description",
                "message",
                "subtitle",
                "text");
        }

        return (header, description);
    }

    private static string TryResolveByTokenAndSuffixes(
        IReadOnlyDictionary<string, string> resourceMap,
        string token,
        params string[] normalizedSuffixes)
    {
        var normalizedToken = NormalizeLookupToken(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return string.Empty;
        }

        var bestKey = resourceMap.Keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => new { Key = key, Normalized = NormalizeLookupToken(key) })
            .Where(x => x.Normalized.StartsWith(normalizedToken, StringComparison.OrdinalIgnoreCase))
            .Where(x => normalizedSuffixes.Contains(GetNormalizedSuffix(x.Normalized), StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.Normalized.Length)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(bestKey) &&
            resourceMap.TryGetValue(bestKey, out var bestValue) &&
            !string.IsNullOrWhiteSpace(bestValue))
        {
            return bestValue.Trim();
        }

        return string.Empty;
    }

    private static string GetNormalizedSuffix(string normalizedKey)
    {
        if (normalizedKey.EndsWith("placeholdertext", StringComparison.OrdinalIgnoreCase))
        {
            return "placeholdertext";
        }

        if (normalizedKey.EndsWith("moduletitle", StringComparison.OrdinalIgnoreCase))
        {
            return "title";
        }

        if (normalizedKey.EndsWith("moduledescription", StringComparison.OrdinalIgnoreCase))
        {
            return "description";
        }

        foreach (var suffix in new[] { "header", "title", "content", "text", "description", "message", "subtitle" })
        {
            if (normalizedKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return suffix;
            }
        }

        return string.Empty;
    }

    private static string NormalizeLookupToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static string GetFirstResourceString(IReadOnlyDictionary<string, string> resourceMap, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetResourceString(resourceMap, key, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool TryGetResourceString(IReadOnlyDictionary<string, string> resourceMap, string key, out string value)
    {
        if (resourceMap.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue.Trim();
            return true;
        }

        var alternate = key.Contains('/')
            ? key.Replace('/', '.')
            : key.Replace('.', '/');

        if (!string.Equals(key, alternate, StringComparison.Ordinal) &&
            resourceMap.TryGetValue(alternate, out rawValue) &&
            !string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, string>? TryLoadResourceMap(string indexPath)
    {
        var candidates = new List<string>();

        var envPath = Environment.GetEnvironmentVariable("SETTINGS_SEARCH_EVAL_RESW");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidates.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(envPath)));
        }

        var indexDirectory = Path.GetDirectoryName(Path.GetFullPath(indexPath));
        if (!string.IsNullOrWhiteSpace(indexDirectory))
        {
            candidates.Add(Path.GetFullPath(Path.Combine(indexDirectory, "..", "..", "Strings", "en-us", "Resources.resw")));
            candidates.Add(Path.GetFullPath(Path.Combine(indexDirectory, "Resources.resw")));

            var repoRoot = FindRepoRoot(indexDirectory);
            if (!string.IsNullOrWhiteSpace(repoRoot))
            {
                candidates.Add(Path.Combine(repoRoot, "src", "settings-ui", "Settings.UI", "Strings", "en-us", "Resources.resw"));
            }
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var loaded = TryLoadResourceMapFromResw(candidate);
            if (loaded.Count > 0)
            {
                return loaded;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> TryLoadResourceMapFromResw(string path)
    {
        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            var root = document.Root;
            if (root == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dataElement in root.Elements("data"))
            {
                var key = dataElement.Attribute("name")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var value = dataElement.Element("value")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                map[key] = value;

                var dotKey = key.Replace('/', '.');
                map[dotKey] = value;

                var slashKey = key.Replace('.', '/');
                map[slashKey] = value;
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? FindRepoRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);
        while (current != null)
        {
            var markerPath = Path.Combine(current.FullName, "PowerToys.slnx");
            if (File.Exists(markerPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex ConsecutiveWhitespaceRegex();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundaryRegex();

    private sealed class RawSettingEntry
    {
        public EntryType Type { get; init; }

        public string? Header { get; init; }

        public string? PageTypeName { get; init; }

        public string? ElementName { get; init; }

        public string? ElementUid { get; init; }

        public string? ParentElementName { get; init; }

        public string? Description { get; init; }

        public string? Icon { get; init; }
    }

    private sealed class RawEvaluationCase
    {
        public string? Query { get; init; }

        public List<string>? ExpectedIds { get; init; }

        public string? Notes { get; init; }
    }
}
