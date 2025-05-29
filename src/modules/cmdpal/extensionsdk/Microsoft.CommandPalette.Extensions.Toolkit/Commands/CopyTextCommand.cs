// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CopyTextCommand : InvokableCommand
{
    public virtual string Text { get; set; }

    public virtual CommandResult Result { get; set; } = CommandResult.ShowToast("Copied to clipboard");

    public CopyTextCommand(string text)
    {
        Text = text;
        Name = "Copy";
        Icon = new IconInfo("\uE8C8");
    }

    public override ICommandResult Invoke()
    {
        ClipboardHelper.SetText(Text);
        return Result;
    }
}
