// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys;

public partial class PowerToysCommandsProvider : CommandProvider
{
    public PowerToysCommandsProvider()
    {
        DisplayName = "PowerToys";
        Icon = new IconInfo("\uE82D"); // TODO: Use proper icon
        Logger.LogInfo("PowerToysCommandsProvider constructed.");
    }

    public override ICommandItem[] TopLevelCommands()
    {
        Logger.LogInfo("PowerToysCommandsProvider.TopLevelCommands invoked.");
        return
        [
            new CommandItem(new ListPage()
            {
                Name = "PowerToys",
                Title = "PowerToys",
                Icon = new IconInfo("\uE82D"),
            })
            {
                Title = "PowerToys",
                Subtitle = "PowerToys commands",
            }
        ];
    }
}
