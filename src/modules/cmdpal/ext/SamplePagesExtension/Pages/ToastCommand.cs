// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class ToastCommand(string message, MessageState state = MessageState.Info) : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        var t = new ToastStatusMessage(new StatusMessage()
        {
            Message = message,
            State = state,
        });
        t.Show();

        return CommandResult.KeepOpen();
    }
}
