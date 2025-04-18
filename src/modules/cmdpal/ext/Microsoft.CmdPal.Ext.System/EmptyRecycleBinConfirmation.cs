// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class EmptyRecycleBinConfirmation : InvokableCommand
{
    public EmptyRecycleBinConfirmation(bool settingEmptyRBSuccesMsg)
    {
        Name = Resources.Microsoft_plugin_command_name_empty;
        _settingEmptyRBSuccesMsg = settingEmptyRBSuccesMsg;
    }

    public override CommandResult Invoke()
    {
        if (ResultHelper.ExecutingEmptyRecycleBinTask)
        {
            return CommandResult.ShowToast(new ToastArgs() { Message = Resources.Microsoft_plugin_sys_RecycleBin_EmptyTaskRunning });
        }

        var confirmArgs = new ConfirmationArgs()
        {
            Title = Resources.Microsoft_plugin_sys_confirmation,
            Description = Resources.EmptyRecycleBin_ConfirmationDialog_Description,
            PrimaryCommand = new EmptyRecycleBinCommand(_settingEmptyRBSuccesMsg),
            IsPrimaryCommandCritical = true,
        };

        return CommandResult.Confirm(confirmArgs);
    }

    private bool _settingEmptyRBSuccesMsg;
}
