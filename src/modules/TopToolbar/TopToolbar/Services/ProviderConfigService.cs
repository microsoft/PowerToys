// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;\r\nusing ManagedCommon;\r\nusing TopToolbar.Models;
using TopToolbar.Providers.Configuration;

namespace TopToolbar.Services;

public sealed class ProviderConfigService
{
    private readonly string _userDirectory;
    private readonly string _defaultDirectory;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public ProviderConfigService(string userDirectory = null, string defaultDirectory = null)
    {
        _userDirectory = string.IsNullOrWhiteSpace(userDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TopToolbar", "Providers")
            : userDirectory;

        _defaultDirectory = string.IsNullOrWhiteSpace(defaultDirectory)
            ? Path.Combine(AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "TopToolbarProviders")
            : defaultDirectory;

        if (!string.IsNullOrEmpty(_userDirectory))
        {
            Directory.CreateDirectory(_userDirectory);
        }
    }

    public IReadOnlyList<ProviderConfig> LoadConfigs()
    {
        var results = new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in EnumerateDirectories())
        {
            foreach (var file in EnumerateJsonFiles(directory))
            {
                try
                {
                    var config = LoadConfig(file);
                    if (config == null || string.IsNullOrWhiteSpace(config.Id))
                    {
                        continue;
                    }

                    results[config.Id] = Normalize(config);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ProviderConfigService: failed to load '{file}' - {ex.Message}.");
                }
            }
        }

        return new ReadOnlyCollection<ProviderConfig>(results.Values.ToList());
    }

    private IEnumerable<string> EnumerateDirectories()
    {
        if (!string.IsNullOrEmpty(_defaultDirectory) && Directory.Exists(_defaultDirectory))
        {
            yield return _defaultDirectory;
        }

        if (!string.IsNullOrEmpty(_userDirectory) && Directory.Exists(_userDirectory))
        {
            yield return _userDirectory;
        }
    }

    private static IEnumerable<string> EnumerateJsonFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static ProviderConfig LoadConfig(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProviderConfig>(stream, JsonOptions);
    }

    private static ProviderConfig Normalize(ProviderConfig config)
    {
        config ??= new ProviderConfig();
        config.Id = config.Id?.Trim() ?? string.Empty;
        config.GroupName = config.GroupName?.Trim() ?? string.Empty;
        config.Description = config.Description?.Trim() ?? string.Empty;
        config.Actions ??= new List<ProviderActionConfig>();

        for (var i = config.Actions.Count - 1; i >= 0; i--)
        {
            var action = config.Actions[i];
            if (action == null)
            {
                config.Actions.RemoveAt(i);
                continue;
            }

            action.Id = action.Id?.Trim() ?? string.Empty;
            action.Name = action.Name?.Trim() ?? string.Empty;
            action.Description = action.Description?.Trim() ?? string.Empty;
            action.IconGlyph = action.IconGlyph?.Trim() ?? string.Empty;
            action.IconPath = action.IconPath?.Trim() ?? string.Empty;

            action.Action ??= new ToolbarAction();
        }

        return config;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}


