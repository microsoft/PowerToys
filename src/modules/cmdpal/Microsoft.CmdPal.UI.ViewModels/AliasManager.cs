// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AliasManager : ObservableObject
{
    private readonly TopLevelCommandManager _topLevelCommandManager;

    // REMEMBER, CommandAlias.SearchPrefix is what we use as keys
    private readonly Dictionary<string, CommandAlias> _aliases;

    public AliasManager(TopLevelCommandManager tlcManager, SettingsModel settings)
    {
        _topLevelCommandManager = tlcManager;
        _aliases = settings.Aliases;

        if (_aliases.Count == 0)
        {
            PopulateDefaultAliases();
        }
    }

    private void AddAlias(CommandAlias a) => _aliases.Add(a.SearchPrefix, a);

    public bool CheckAlias(string searchText)
    {
        if (_aliases.TryGetValue(searchText, out var alias))
        {
            try
            {
                var topLevelCommand = _topLevelCommandManager.LookupCommand(alias.CommandId);
                if (topLevelCommand != null)
                {
                    WeakReferenceMessenger.Default.Send<ClearSearchMessage>();
                    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(topLevelCommand));
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
        this.AddAlias(new CommandAlias(":", "com.microsoft.cmdpal.registry", true));
        this.AddAlias(new CommandAlias("$", "com.microsoft.cmdpal.windowsSettings", true));
        this.AddAlias(new CommandAlias("=", "com.microsoft.cmdpal.calculator", true));
        this.AddAlias(new CommandAlias(">", "com.microsoft.cmdpal.shell", true));
        this.AddAlias(new CommandAlias("<", "com.microsoft.cmdpal.windowwalker", true));
        this.AddAlias(new CommandAlias("??", "com.microsoft.cmdpal.websearch", true));
        this.AddAlias(new CommandAlias("file", "com.microsoft.indexer.fileSearch", false));
        this.AddAlias(new CommandAlias(")", "com.microsoft.cmdpal.timedate", true));
    }

    public string? KeysFromId(string commandId)
    {
        return _aliases
            .Where(kv => kv.Value.CommandId == commandId)
            .Select(kv => kv.Value.Alias)
            .FirstOrDefault();
    }

    public CommandAlias? AliasFromId(string commandId)
    {
        return _aliases
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

        // If we already have _this exact alias_, do nothing
        if (newAlias != null &&
            _aliases.TryGetValue(newAlias.SearchPrefix, out var existingAlias))
        {
            if (existingAlias.CommandId == commandId)
            {
                return;
            }
        }

        // Look for the old alias, and remove it
        List<CommandAlias> toRemove = [];
        foreach (var kv in _aliases)
        {
            if (kv.Value.CommandId == commandId)
            {
                toRemove.Add(kv.Value);
            }
        }

        foreach (var alias in toRemove)
        {
            // REMEMBER, SearchPrefix is what we use as keys
            _aliases.Remove(alias.SearchPrefix);
        }

        if (newAlias != null)
        {
            AddAlias(newAlias);
        }
    }
}
