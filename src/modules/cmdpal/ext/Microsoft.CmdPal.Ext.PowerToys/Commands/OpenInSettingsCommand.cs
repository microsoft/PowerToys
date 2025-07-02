// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

internal sealed partial class OpenInSettingsCommand : InvokableCommand
{
    private readonly PowerToysModuleEntry _entry;

    public OpenInSettingsCommand(PowerToysModuleEntry entry)
    {
        _entry = entry;
        Name = Resources.PowerToysProvider_DisplayName;
    }

    public override CommandResult Invoke()
    {
        _entry.NavigateToSettingsPage();
        return CommandResult.KeepOpen();
    }
}
