// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AliasManager : ObservableObject
{
    private const string DeprecatedShellCommandId = "com.microsoft.cmdpal.shell";

    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;

    private static readonly ImmutableList<CommandAlias> _defaultAliases = new List<CommandAlias>
    {
        new CommandAlias(":", BuiltInCommandIds.Registry, true),
        new CommandAlias("$", BuiltInCommandIds.WindowsSettings, true),
        new CommandAlias("=", BuiltInCommandIds.Calculator, true),
        new CommandAlias(">", BuiltInCommandIds.Run, true),
        new CommandAlias("<", BuiltInCommandIds.WindowWalker, true),
        new CommandAlias("??", BuiltInCommandIds.WebSearch, true),
        new CommandAlias("file", BuiltInCommandIds.FileSearch, false),
        new CommandAlias(")", BuiltInCommandIds.TimeDate, true),
    }.ToImmutableList();

    public AliasManager(TopLevelCommandManager tlcManager, ISettingsService settingsService)
    {
        _topLevelCommandManager = tlcManager;
        _settingsService = settingsService;

        MigrateRenamedRunAliases();

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
                    .AddRange(_defaultAliases.ToDictionary(a => a.SearchPrefix, a => a)),
            },
            hotReload: false);
    }

    private void MigrateRenamedRunAliases()
    {
        var aliases = _settingsService.Settings.Aliases;
        var migratedAliases = MigrateRenamedRunAliases(aliases);
        if (ReferenceEquals(aliases, migratedAliases))
        {
            return;
        }

        _settingsService.UpdateSettings(
            static s => s with { Aliases = MigrateRenamedRunAliases(s.Aliases) },
            hotReload: false);
    }

    private static ImmutableDictionary<string, CommandAlias> MigrateRenamedRunAliases(ImmutableDictionary<string, CommandAlias> aliases)
    {
        var runCommandHasAlias = aliases.Values.Any(alias => alias.CommandId == BuiltInCommandIds.Run);
        var migratedAliases = aliases;
        foreach (var alias in aliases)
        {
            if (alias.Value.CommandId == DeprecatedShellCommandId)
            {
                migratedAliases = runCommandHasAlias
                    ? migratedAliases.Remove(alias.Key)
                    : migratedAliases.SetItem(alias.Key, alias.Value with { CommandId = BuiltInCommandIds.Run });
            }
        }

        return migratedAliases;
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
