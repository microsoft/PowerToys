// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed partial class SendMessageCommand : InvokableCommand
{
    private static int sentMessages;

    public override ICommandResult Invoke()
    {
        var kind = MessageState.Info;
        switch (sentMessages % 4)
        {
            case 0: kind = MessageState.Info; break;
            case 1: kind = MessageState.Success; break;
            case 2: kind = MessageState.Warning; break;
            case 3: kind = MessageState.Error; break;
        }

        var message = new StatusMessage() { Message = $"I am status message no.{sentMessages++}", State = kind };
        ExtensionHost.ShowStatus(message);
        return CommandResult.KeepOpen();
    }
}
