// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class CopyTextCommand : InvokableCommand
{
    public string Text { get; set; }

    public CommandResult Result { get; set; } = CommandResult.Dismiss();

    public CopyTextCommand(string text)
    {
        Text = text;
        Name = "Copy";
        Icon = new("\uE8C8");
    }

    public override ICommandResult Invoke()
    {
        ClipboardHelper.SetText(Text);
        return Result;
    }
}
