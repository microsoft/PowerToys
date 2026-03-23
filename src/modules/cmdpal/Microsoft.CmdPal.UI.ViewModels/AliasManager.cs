// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AliasManager : ObservableObject
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;

    public AliasManager(TopLevelCommandManager tlcManager, ISettingsService settingsService)
    {
        _topLevelCommandManager = tlcManager;
        _settingsService = settingsService;

        if (_settingsService.Settings.Aliases.Count == 0)
        {
            PopulateDefaultAliases();
        }
    }

    public bool CheckAlias(string searchText)
    {
        if (_settingsService.Settings.Aliases.TryGetValue(searchText, out var alias))
        {
            try
            {
                var topLevelCommand = _topLevelCommandManager.LookupCommand(alias.CommandId);
                if (topLevelCommand is not null)
                {
                    WeakReferenceMessenger.Default.Send<ClearSearchMessage>();

                    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(topLevelCommand.GetPerformCommandMessage());
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private void PopulateDefaultAliases()
    {
        _settingsService.UpdateSettings(
            s => s with
            {
                Aliases = s.Aliases
                    .Add(":", new CommandAlias(":", "com.microsoft.cmdpal.registry", true))
                    .Add("$", new CommandAlias("$", "com.microsoft.cmdpal.windowsSettings", true))
                    .Add("=", new CommandAlias("=", "com.microsoft.cmdpal.calculator", true))
                    .Add(">", new CommandAlias(">", "com.microsoft.cmdpal.shell", true))
                    .Add("<", new CommandAlias("<", "com.microsoft.cmdpal.windowwalker", true))
                    .Add("??", new CommandAlias("??", "com.microsoft.cmdpal.websearch", true))
                    .Add("file", new CommandAlias("file", "com.microsoft.indexer.fileSearch", false))
                    .Add(")", new CommandAlias(")", "com.microsoft.cmdpal.timedate", true)),
            },
            hotReload: false);
    }

    public string? KeysFromId(string commandId)
    {
        return _settingsService.Settings.Aliases
            .Where(kv => kv.Value.CommandId == commandId)
            .Select(kv => kv.Value.Alias)
            .FirstOrDefault();
    }

    public CommandAlias? AliasFromId(string commandId)
    {
        return _settingsService.Settings.Aliases
            .Where(kv => kv.Value.CommandId == commandId)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    public void UpdateAlias(string commandId, CommandAlias? newAlias)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            // do nothing?
            return;
        }

        var aliases = _settingsService.Settings.Aliases;

        // If we already have _this exact alias_, do nothing
        if (newAlias is not null &&
            aliases.TryGetValue(newAlias.SearchPrefix, out var existingAlias))
        {
            if (existingAlias.CommandId == commandId)
            {
                return;
            }
        }

        var keysToRemove = new List<string>();
        foreach (var kv in aliases)
        {
            // Look for the old aliases for the command, and remove it
            if (kv.Value.CommandId == commandId)
            {
                keysToRemove.Add(kv.Key);
            }

            // Look for the alias belonging to another command, and remove it
            if (newAlias is not null && kv.Value.Alias == newAlias.Alias && kv.Value.CommandId != commandId)
            {
                keysToRemove.Add(kv.Key);

                // Remove alias from other TopLevelViewModels it may be assigned to
                var topLevelCommand = _topLevelCommandManager.LookupCommand(kv.Value.CommandId);
                if (topLevelCommand is not null)
                {
                    topLevelCommand.AliasText = string.Empty;
                }
            }
        }

        _settingsService.UpdateSettings(s =>
        {
            var updatedAliases = s.Aliases.RemoveRange(keysToRemove);

            if (newAlias is not null)
            {
                updatedAliases = updatedAliases.Add(newAlias.SearchPrefix, newAlias);
            }

            return s with { Aliases = updatedAliases };
        });
    }
}
