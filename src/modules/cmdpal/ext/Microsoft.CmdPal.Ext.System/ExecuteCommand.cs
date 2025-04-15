// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class ExecuteCommand : InvokableCommand
{
    public ExecuteCommand(Action command)
    {
        _command = command;
    }

    public override CommandResult Invoke()
    {
        _command();
        return CommandResult.Dismiss();
    }

    private Action _command;
}
