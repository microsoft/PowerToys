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
    private readonly Dictionary<string, CommandAlias> _aliases = [];

    public AliasManager(TopLevelCommandManager tlcManager)
    {
        _topLevelCommandManager = tlcManager;
        PopulateDefaultAliases();
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
                    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(new(topLevelCommand.Command)));
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
}
