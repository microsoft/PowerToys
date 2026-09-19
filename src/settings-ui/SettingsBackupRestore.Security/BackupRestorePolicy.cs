// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Parses the existing backup_restore_settings.json contract without changing its archive behavior.
/// </summary>
public sealed class BackupRestorePolicy
{
    private readonly string[] includePatterns;
    private readonly string[] ignorePatterns;
    private readonly SortedSet<string> overwritePaths;
    private readonly Dictionary<string, HashSet<string>> ignoredSettings;
    private readonly Dictionary<string, HashSet<string>> ignoredPowerToysRunSettings;

    private BackupRestorePolicy(
        string[] includePatterns,
        string[] ignorePatterns,
        SortedSet<string> overwritePaths,
        Dictionary<string, HashSet<string>> ignoredSettings,
        Dictionary<string, HashSet<string>> ignoredPowerToysRunSettings,
        bool restartAfterRestore)
    {
        this.includePatterns = includePatterns;
        this.ignorePatterns = ignorePatterns;
        this.overwritePaths = overwritePaths;
        this.ignoredSettings = ignoredSettings;
        this.ignoredPowerToysRunSettings = ignoredPowerToysRunSettings;
        RestartAfterRestore = restartAfterRestore;
    }

    /// <summary>
    /// Gets whether the existing contract requests a PowerToys restart after restore.
    /// </summary>
    public bool RestartAfterRestore { get; }

    /// <summary>
    /// Parses the production backup/restore configuration.
    /// </summary>
    public static BackupRestorePolicy Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        JsonNode root = JsonNode.Parse(json) ?? throw new JsonException("Backup/restore configuration is empty.");

        string[] includes = ReadStringArray(root["IncludeFiles"]);
        string[] ignores = ReadStringArray(root["IgnoreFiles"]);
        SortedSet<string> overwrites = new(WindowsPathComparer.Instance);
        if (root["CustomRestoreSettings"] is JsonObject customRestoreSettings)
        {
            foreach ((string path, JsonNode? value) in customRestoreSettings)
            {
                if (value?["overwrite"]?.GetValue<bool>() == true)
                {
                    overwrites.Add(NormalizePolicyPath(path));
                }
            }
        }

        Dictionary<string, HashSet<string>> ignored = new(StringComparer.Ordinal);
        if (root["IgnoredSettings"] is JsonObject ignoredSettings)
        {
            foreach ((string path, JsonNode? value) in ignoredSettings)
            {
                ignored[path] = new HashSet<string>(ReadStringArray(value), StringComparer.Ordinal);
            }
        }

        Dictionary<string, HashSet<string>> ignoredRunSettings = new(StringComparer.Ordinal);
        if (root["IgnoredPTRunSettings"] is JsonArray ignoredPlugins)
        {
            foreach (JsonNode? node in ignoredPlugins)
            {
                if (node is JsonObject plugin &&
                    plugin["Id"]?.GetValue<string>() is string id)
                {
                    ignoredRunSettings[id] = new HashSet<string>(ReadStringArray(plugin["Names"]), StringComparer.Ordinal);
                }
            }
        }

        bool restart = root[nameof(RestartAfterRestore)]?.GetValue<bool>() ?? true;
        return new BackupRestorePolicy(includes, ignores, overwrites, ignored, ignoredRunSettings, restart);
    }

    /// <summary>
    /// Applies the production IncludeFiles and IgnoreFiles wildcard rules.
    /// </summary>
    public bool ShouldInclude(string relativePath)
    {
        string policyPath = NormalizePolicyPath(relativePath);
        bool included = includePatterns.Any(pattern => Regex.IsMatch(policyPath, WildcardToRegex(pattern)));
        return included && !IsIgnored(policyPath);
    }

    /// <summary>
    /// Returns whether a path is excluded by the production IgnoreFiles rules.
    /// </summary>
    public bool IsIgnored(string relativePath)
    {
        string policyPath = NormalizePolicyPath(relativePath);
        return ignorePatterns.Any(pattern => Regex.IsMatch(policyPath, WildcardToRegex(pattern)));
    }

    /// <summary>
    /// Returns merge or overwrite according to CustomRestoreSettings.
    /// </summary>
    public RestoreMode GetRestoreMode(string relativePath)
    {
        return overwritePaths.Contains(NormalizePolicyPath(relativePath)) ? RestoreMode.Overwrite : RestoreMode.Merge;
    }

    /// <summary>
    /// Applies the production top-level and PowerToys Run export exclusions to in-memory JSON.
    /// </summary>
    public string CreateExportVersion(string relativePath, string settingsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsJson);

        string policyPath = NormalizePolicyPath(relativePath);
        string policyKey = policyPath.TrimStart('\\');
        HashSet<string> excluded = ignoredSettings.GetValueOrDefault(policyKey) ?? [];
        JsonObject source = JsonNode.Parse(settingsJson) as JsonObject ??
                            throw new JsonException("Settings JSON must be an object.");
        JsonObject exported = new();
        foreach ((string name, JsonNode? value) in source.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            if (!excluded.Contains(name))
            {
                exported[name] = value?.DeepClone();
            }
        }

        if (policyPath.Equals(@"\PowerToys Run\settings.json", StringComparison.OrdinalIgnoreCase) &&
            exported["plugins"] is JsonArray plugins)
        {
            foreach (JsonNode? node in plugins)
            {
                if (node is JsonObject plugin &&
                    plugin["Id"]?.GetValue<string>() is string id &&
                    ignoredPowerToysRunSettings.TryGetValue(id, out HashSet<string>? propertyNames))
                {
                    foreach (string propertyName in propertyNames)
                    {
                        plugin.Remove(propertyName);
                    }
                }
            }
        }

        return exported.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] ReadStringArray(JsonNode? node)
    {
        return node is JsonArray array
            ? array.Select(item => item?.GetValue<string>() ?? string.Empty).ToArray()
            : [];
    }

    private static string NormalizePolicyPath(string path)
    {
        string normalized = SecurePath.NormalizeRelative(path.TrimStart('\\', '/'));
        return "\\" + normalized;
    }

    private static string WildcardToRegex(string wildcard)
    {
        return "^" + Regex.Escape(wildcard).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
    }
}
