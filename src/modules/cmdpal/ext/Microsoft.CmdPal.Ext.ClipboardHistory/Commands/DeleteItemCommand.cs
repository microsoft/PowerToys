// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class DeleteItemCommand : InvokableCommand
{
    private readonly ClipboardItem _clipboardItem;

    internal DeleteItemCommand(ClipboardItem clipboardItem)
    {
        _clipboardItem = clipboardItem;
        Name = Properties.Resources.delete_command_name;
        Icon = Icons.DeleteIcon;
    }

    public override CommandResult Invoke()
    {
        Clipboard.DeleteItemFromHistory(_clipboardItem.Item);
        return CommandResult.ShowToast(Properties.Resources.delete_toast_text);
    }
}
