// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerToys.Helper;
using Microsoft.CmdPal.Ext.PowerToys.Pages;
using Microsoft.CmdPal.Ext.PowerToys.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys;

public partial class PowerToysCommandsProvider : CommandProvider
{
    public PowerToysCommandsProvider()
    {
        Id = "Microsoft.PowerToys";
        DisplayName = Resources.PowerToysProvider_DisplayName;
        Icon = PowerToysResourcesHelper.ProviderIcon();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new PowerToysListPage())
            {
                Title = Resources.PowerToysPage_Title,
                Subtitle = Resources.PowerToysPage_Subtitle,
            }
        ];
    }
}
