// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsServices;

public partial class WindowsServicesCommandsProvider : CommandProvider
{
    public WindowsServicesCommandsProvider()
    {
        Id = "Windows.Services";
        DisplayName = $"Windows Services";
        Icon = new("%windir%\\system32\\filemgmt.dll");
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
