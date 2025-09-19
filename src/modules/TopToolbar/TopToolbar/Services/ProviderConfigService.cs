// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using TopToolbar.Models;
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
        config.Layout ??= new ProviderLayoutConfig();
        config.External ??= new ExternalProviderConfig();
        NormalizeExternal(config.External);

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

            if (action.Action.Type == ToolbarActionType.Provider)
            {
                action.Action.ProviderId = string.IsNullOrWhiteSpace(action.Action.ProviderId)
                    ? config.Id
                    : action.Action.ProviderId.Trim();

                action.Action.ProviderActionId = action.Action.ProviderActionId?.Trim() ?? string.Empty;
                action.Action.ProviderArgumentsJson = action.Action.ProviderArgumentsJson?.Trim();
            }

            action.Input ??= new ProviderActionInputConfig();
            NormalizeInput(action.Input);
        }

        return config;
    }

    private static void NormalizeInput(ProviderActionInputConfig input)
    {
        if (input == null)
        {
            return;
        }

        input.LabelTemplate = input.LabelTemplate?.Trim() ?? string.Empty;
        input.DescriptionTemplate = input.DescriptionTemplate?.Trim() ?? string.Empty;
        input.IconGlyph = input.IconGlyph?.Trim() ?? string.Empty;
        input.IconPath = input.IconPath?.Trim() ?? string.Empty;
        input.Enum ??= new List<ProviderActionEnumOption>();

        for (var i = input.Enum.Count - 1; i >= 0; i--)
        {
            var option = input.Enum[i];
            if (option == null)
            {
                input.Enum.RemoveAt(i);
                continue;
            }

            option.Label = option.Label?.Trim() ?? string.Empty;
            option.Value = option.Value?.Trim() ?? string.Empty;
            option.Description = option.Description?.Trim() ?? string.Empty;
            option.IconGlyph = option.IconGlyph?.Trim() ?? string.Empty;
            option.IconPath = option.IconPath?.Trim() ?? string.Empty;
        }

        if (input.Dynamic != null)
        {
            NormalizeDynamicInput(input.Dynamic);
        }
    }

    private static void NormalizeDynamicInput(ProviderActionDynamicInputConfig dynamic)
    {
        if (dynamic == null)
        {
            return;
        }

        dynamic.SourceTool = dynamic.SourceTool?.Trim() ?? string.Empty;
        dynamic.ItemsPath = string.IsNullOrWhiteSpace(dynamic.ItemsPath) ? "content[0].data" : dynamic.ItemsPath.Trim();
        dynamic.LabelField = dynamic.LabelField?.Trim() ?? string.Empty;
        dynamic.LabelTemplate = dynamic.LabelTemplate?.Trim() ?? string.Empty;
        dynamic.DescriptionField = dynamic.DescriptionField?.Trim() ?? string.Empty;
        dynamic.DescriptionTemplate = dynamic.DescriptionTemplate?.Trim() ?? string.Empty;
        dynamic.ValueField = dynamic.ValueField?.Trim() ?? string.Empty;
        dynamic.IconGlyphField = dynamic.IconGlyphField?.Trim() ?? string.Empty;
        dynamic.IconPathField = dynamic.IconPathField?.Trim() ?? string.Empty;
        dynamic.SortField = dynamic.SortField?.Trim() ?? string.Empty;
    }

    private static void NormalizeExternal(ExternalProviderConfig external)
    {
        if (external == null)
        {
            return;
        }

        external.ExecutablePath = external.ExecutablePath?.Trim() ?? string.Empty;
        external.Arguments = external.Arguments?.Trim() ?? string.Empty;
        external.WorkingDirectory = external.WorkingDirectory?.Trim() ?? string.Empty;
        external.Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (external.Environment.Count > 0)
        {
            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in external.Environment)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                normalized[pair.Key.Trim()] = pair.Value?.Trim() ?? string.Empty;
            }

            external.Environment = normalized;
        }
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
