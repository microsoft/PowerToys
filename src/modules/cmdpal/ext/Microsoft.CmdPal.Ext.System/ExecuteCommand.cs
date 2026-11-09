// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class ExecuteCommand : InvokableCommand
{
    public ExecuteCommand(Action command)
        : this(() =>
        {
            command();
            return CommandResult.Dismiss();
        })
    {
    }

    public ExecuteCommand(Func<CommandResult> command)
    {
        _command = command;
    }

    public override CommandResult Invoke() => _command();

    private Func<CommandResult> _command;
}
