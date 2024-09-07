// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette.Views;

public sealed class NoOpAction : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
