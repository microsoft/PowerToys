// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace SpongebotExtension;

public class CopyTextAction : InvokableCommand
{
    internal string Text { get; set; }

    public CopyTextAction(string text)
    {
        Text = text;
        Name = "Copy";
        Icon = new("\uE8C8");
    }

    public override ICommandResult Invoke()
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(Text);
        Clipboard.SetContent(dataPackage);

        // ClipboardHelper.SetText(Text);
        return CommandResult.KeepOpen();
    }
}
