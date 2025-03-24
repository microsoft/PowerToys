// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsServices.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsServices;

public partial class WindowsServicesCommandsProvider : CommandProvider
{
    // For giggles, "%windir%\\system32\\filemgmt.dll" also _just works_.
    public static IconInfo ServicesIcon { get; } = new("\ue9f5");

    public WindowsServicesCommandsProvider()
    {
        Id = "Windows.Services";
        DisplayName = Resources.WindowsServicesProvider_DisplayName;
        Icon = ServicesIcon;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new ServicesListPage())
            {
                Title = "Windows Services",
                Subtitle = "Manage Windows Services",
            }
        ];
    }
}
