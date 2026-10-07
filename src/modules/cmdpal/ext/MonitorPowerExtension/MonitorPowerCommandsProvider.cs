// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using MonitorPowerExtension.Properties;

namespace MonitorPowerExtension;

public partial class MonitorPowerCommandsProvider : CommandProvider
{
    private readonly CommandItem _pageItem;

    public MonitorPowerCommandsProvider()
    {
        Id = "MonitorPower";
        DisplayName = Resources.provider_display_name;
        Icon = new IconInfo("\uE7F4");

        var page = new Pages.MonitorPowerListPage();
        _pageItem = new CommandItem(page)
        {
            Title = Resources.provider_display_name,
            Subtitle = Resources.provider_subtitle,
            Icon = Icon,
        };

        DisplayHelpers.EnableXboxGuideViewCombo();
    }

    public override ICommandItem[] TopLevelCommands() => [_pageItem];
}
