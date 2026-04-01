// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class RefreshAwakeStatusCommand : InvokableCommand
{
    private readonly Action _refreshAction;

    internal RefreshAwakeStatusCommand(Action refreshAction)
    {
        ArgumentNullException.ThrowIfNull(refreshAction);
        _refreshAction = refreshAction;
        Name = "Refresh Awake status";
    }

    public override CommandResult Invoke()
    {
        _refreshAction();
        return CommandResult.KeepOpen();
    }
}
