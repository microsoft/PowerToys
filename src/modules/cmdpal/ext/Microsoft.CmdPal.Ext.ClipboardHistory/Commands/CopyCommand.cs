// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class CopyCommand : InvokableCommand
{
    private readonly ClipboardItem _clipboardItem;
    private readonly ClipboardFormat _clipboardFormat;

    internal CopyCommand(ClipboardItem clipboardItem, ClipboardFormat clipboardFormat)
    {
        _clipboardItem = clipboardItem;
        _clipboardFormat = clipboardFormat;
        Name = Properties.Resources.copy_command_name;
        if (clipboardFormat == ClipboardFormat.Text)
        {
            Icon = Icons.CopyIcon;
        }
        else
        {
            Icon = Icons.PictureIcon;
        }
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetClipboardContent(_clipboardItem, _clipboardFormat);
        return CommandResult.ShowToast(Properties.Resources.copied_toast_text);
    }
}
