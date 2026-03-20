// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

public partial class PerformanceMonitorCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem _band;
    private readonly SettingsManager _settingsManager = new();

    public PerformanceMonitorCommandsProvider()
    {
        DisplayName = Resources.GetResource("Performance_Monitor_Title");
        Id = "PerformanceMonitor";
        Icon = Icons.PerformanceMonitorIcon;

        var page = new PerformanceWidgetsPage(_settingsManager, false);
        var band = new PerformanceWidgetsPage(_settingsManager, true);
        _band = new CommandItem(band) { Title = DisplayName };
        _commands = [
            new CommandItem(page)
            {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];

        Settings = _settingsManager.Settings;
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
