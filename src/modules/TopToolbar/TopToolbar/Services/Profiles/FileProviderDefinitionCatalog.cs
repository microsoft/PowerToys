// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TopToolbar.Logging;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// File-based implementation of <see cref="IProviderDefinitionCatalog"/>.
/// Scans a providers directory for *.json definitions.
/// </summary>
public sealed class FileProviderDefinitionCatalog : IProviderDefinitionCatalog
{
    private readonly string _providersDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileProviderDefinitionCatalog(string providersDirectory = null)
    {
        _providersDirectory = string.IsNullOrWhiteSpace(providersDirectory)
            ? AppPaths.ProviderDefinitionsDirectory
            : providersDirectory;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        try
        {
            Directory.CreateDirectory(_providersDirectory);
        }
        catch
        {
            // Ignore directory creation failures; LoadAll will surface issues.
        }
    }

    public IReadOnlyDictionary<string, ProviderDefinitionFile> LoadAll()
    {
        var result = new Dictionary<string, ProviderDefinitionFile>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_providersDirectory))
        {
            return result;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(_providersDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"ProviderCatalog: enumerate failed - {ex.Message}.");
            return result;
        }

        foreach (var file in files)
        {
            try
            {
                using var stream = File.OpenRead(file);
                var def = JsonSerializer.Deserialize<ProviderDefinitionFile>(stream, _jsonOptions);
                if (def == null || string.IsNullOrWhiteSpace(def.ProviderId))
                {
                    continue;
                }

                Normalize(def);

                if (result.ContainsKey(def.ProviderId))
                {
                    AppLogger.LogWarning($"ProviderCatalog: duplicate providerId '{def.ProviderId}' in '{file}', ignoring.");
                    continue;
                }

                result[def.ProviderId] = def;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"ProviderCatalog: failed to load '{file}' - {ex.Message}.");
            }
        }

        return result;
    }

    private static void Normalize(ProviderDefinitionFile def)
    {
        def.ProviderId = def.ProviderId?.Trim() ?? string.Empty;
        def.DisplayName = def.DisplayName?.Trim() ?? def.ProviderId;
        def.Description = def.Description?.Trim() ?? string.Empty;
        def.Groups ??= new List<ProviderGroupDef>();

        foreach (var g in def.Groups.ToList())
        {
            if (g == null)
            {
                def.Groups.Remove(g);
                continue;
            }

            g.GroupId = g.GroupId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(g.GroupId))
            {
                def.Groups.Remove(g);
                continue;
            }

            g.DisplayName = g.DisplayName?.Trim() ?? g.GroupId;
            g.Actions ??= new List<ProviderActionDef>();

            foreach (var a in g.Actions.ToList())
            {
                if (a == null)
                {
                    g.Actions.Remove(a);
                    continue;
                }

                a.Name = a.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(a.Name))
                {
                    g.Actions.Remove(a);
                    continue;
                }

                a.DisplayName = a.DisplayName?.Trim() ?? a.Name;
                a.IconGlyph = a.IconGlyph?.Trim() ?? string.Empty;
                a.IconPath = a.IconPath?.Trim() ?? string.Empty;
            }
        }
    }
}
