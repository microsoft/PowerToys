// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class ExecuteCommandConfirmation : InvokableCommand
{
    public ExecuteCommandConfirmation(string name, bool confirm, string confirmationMessage, Action command)
    {
        Name = name;
        _command = command;
        _confirm = confirm;
        _confirmationMessage = confirmationMessage;
    }

    public override CommandResult Invoke()
    {
        if (_confirm)
        {
            var confirmationArgs = new ConfirmationArgs
            {
                Title = Resources.Microsoft_plugin_sys_confirmation,
                Description = _confirmationMessage,
                PrimaryCommand = new ExecuteCommand(_command),
                IsPrimaryCommandCritical = true,
            };

            return CommandResult.Confirm(confirmationArgs);
        }

        _command();
        return CommandResult.Dismiss();
    }

    private bool _confirm;
    private string _confirmationMessage;
    private Action _command;
}
