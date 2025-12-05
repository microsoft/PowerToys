// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

public partial class PerformanceMonitorCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem _band;

    public PerformanceMonitorCommandsProvider()
    {
        DisplayName = "Performance Monitor";
        Id = "PerformanceMonitor";
        Icon = Icons.StackedAreaIcon;

        var page = new PerformanceMonitorPage(false);
        var band = new PerformanceMonitorPage(true);
        _band = new CommandItem(band) { Title = "Performance monitor" }; // TODO!Loc
        _commands = [
            new CommandItem(page) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[]? GetDockBands()
    {
        return new ICommandItem[] { _band };
    }
}
