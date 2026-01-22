// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Pages;

internal sealed partial class TerminalChannelFilters : Filters
{
    internal const string AllTerminalsFilterId = "all";

    private readonly List<TerminalPackage> _terminals;

    public bool IsAllSelected => CurrentFilterId == AllTerminalsFilterId;

    public TerminalChannelFilters(IEnumerable<TerminalPackage> terminals, string preselectedFilterId = AllTerminalsFilterId)
    {
        CurrentFilterId = preselectedFilterId;
        _terminals = [.. terminals];
    }

    public override IFilterItem[] GetFilters()
    {
        var items = new List<IFilterItem>
        {
            new Filter()
            {
                Id = AllTerminalsFilterId,
                Name = Resources.all_channels,
                Icon = Icons.FilterIcon,
            },
            new Separator(),
        };

        foreach (var terminalPackage in _terminals)
        {
            items.Add(new Filter()
            {
                Id = terminalPackage.AppUserModelId,
                Name = terminalPackage.DisplayName,
                Icon = new IconInfo(terminalPackage.LogoPath),
            });
        }

        return [.. items];
    }

    public bool ContainsFilter(string id)
    {
        return _terminals.FindIndex(terminal => terminal.AppUserModelId == id) > -1;
    }
}
