// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AliasManager : ObservableObject
{
    private readonly SettingsService _settingsService;

    public AliasManager(TopLevelCommandManager tlcManager, SettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = _settingsService.CurrentSettings;

        if (settings.Aliases.Count == 0)
        {
            PopulateDefaultAliases();
        }
    }

    public CommandAlias? CheckAlias(string searchText)
    {
        if (_settingsService.CurrentSettings.Aliases.TryGetValue(searchText, out var alias))
        {
            return alias;
        }

        return null;
    }

    private void PopulateDefaultAliases()
    {
        Dictionary<string, CommandAlias> aliases = new()
            {
                {
                    ":", new CommandAlias(":", "com.microsoft.cmdpal.registry", true)
                },
                {
                    "$", new CommandAlias("$", "com.microsoft.cmdpal.windowsSettings", true)
                },
                {
                    "=", new CommandAlias("=", "com.microsoft.cmdpal.calculator", true)
                },
                {
                    ">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true)
                },
                {
                    "<", new CommandAlias("<", "com.microsoft.cmdpal.windowwalker", true)
                },
                {
                    "??", new CommandAlias("??", "com.microsoft.cmdpal.websearch", true)
                },
                {
                    "file", new CommandAlias("file", "com.microsoft.indexer.fileSearch", false)
                },
                {
                    ")", new CommandAlias(")", "com.microsoft.cmdpal.timedate", true)
                },
            };

        _settingsService.SaveSettings(_settingsService.CurrentSettings with { Aliases = aliases.ToImmutableDictionary() });
    }

    public string? KeysFromId(string commandId)
    {
        return _settingsService.CurrentSettings.Aliases
            .FirstOrDefault(kv => kv.Value.CommandId == commandId)
            .Value
            .Alias;
    }

    public CommandAlias? AliasFromId(string commandId)
    {
        return _settingsService.CurrentSettings.Aliases
            .FirstOrDefault(kv => kv.Value.CommandId == commandId)
            .Value;
    }

    public void UpdateAlias(string commandId, CommandAlias? newAlias)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            // do nothing?
            return;
        }

        // If we already have _this exact alias_, do nothing
        if (newAlias is not null &&
            _settingsService.CurrentSettings.Aliases.TryGetValue(newAlias.SearchPrefix, out var existingAlias))
        {
            if (existingAlias.CommandId == commandId)
            {
                return;
            }
        }

        List<CommandAlias> toRemove = [];
        foreach (var kv in _settingsService.CurrentSettings.Aliases)
        {
            // Look for the old aliases for the command, and remove it
            if (kv.Value.CommandId == commandId)
            {
                toRemove.Add(kv.Value);
            }

            // Look for the alias belonging to another command, and remove it
            if (newAlias is not null && kv.Value.Alias == newAlias.Alias && kv.Value.CommandId != commandId)
            {
                toRemove.Add(kv.Value);
            }
        }

        var aliases = _settingsService.CurrentSettings.Aliases.ToDictionary();

        foreach (var alias in toRemove)
        {
            // REMEMBER, SearchPrefix is what we use as keys
            aliases.Remove(alias.SearchPrefix);
        }

        if (newAlias is not null)
        {
            aliases.Add(newAlias.SearchPrefix, newAlias);
        }

        _settingsService.SaveSettings(_settingsService.CurrentSettings with { Aliases = aliases.ToImmutableDictionary() });
    }
}
