// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerMode.Commands;

internal sealed partial class RefreshPowerModeStatusCommand : InvokableCommand
{
    private readonly Action _refreshAction;

    internal RefreshPowerModeStatusCommand(Action refreshAction)
    {
        ArgumentNullException.ThrowIfNull(refreshAction);
        _refreshAction = refreshAction;
        Name = Resources.power_mode_status_command_name;
    }

    public override CommandResult Invoke()
    {
        _refreshAction();
        return CommandResult.KeepOpen();
    }
}
