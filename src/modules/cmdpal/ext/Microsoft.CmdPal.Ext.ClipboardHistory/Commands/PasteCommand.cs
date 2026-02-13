// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Messages;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class PasteCommand : InvokableCommand
{
    private readonly ClipboardItem _clipboardItem;
    private readonly ClipboardFormat _clipboardFormat;
    private readonly ISettingOptions _settings;

    internal PasteCommand(ClipboardItem clipboardItem, ClipboardFormat clipboardFormat, ISettingOptions settings)
    {
        _clipboardItem = clipboardItem;
        _clipboardFormat = clipboardFormat;
        _settings = settings;
        Name = Properties.Resources.paste_command_name;
        Icon = Icons.PasteIcon;
    }

    private void HideWindow()
    {
        // TODO GH #524: This isn't great - this requires us to have Secret Sauce in
        // the clipboard extension to be able to manipulate the HWND.
        // We probably need to put some window manipulation into the API, but
        // what form that takes is not clear yet.
        WeakReferenceMessenger.Default.Send<HideWindowMessage>(new());
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetClipboardContent(_clipboardItem, _clipboardFormat);
        HideWindow();

        ClipboardHelper.SendPasteKeyCombination();

        if (!_settings.KeepAfterPaste)
        {
            Clipboard.DeleteItemFromHistory(_clipboardItem.Item);
        }

        return CommandResult.ShowToast(Properties.Resources.paste_toast_text);
    }
}
