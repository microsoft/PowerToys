// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
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

        // The hide is processed asynchronously on the UI thread, while Invoke runs on a
        // background thread. Wait (bounded) for another app to take the foreground, otherwise
        // Ctrl+V can land in Command Palette itself instead of the target app.
        if (!ForegroundWindowWaiter.WaitForForegroundToLeaveCurrentProcess(out var waited))
        {
            // Don't paste into Command Palette, and keep the history item since nothing was pasted.
            // The content is still on the clipboard, so the user can paste it manually.
            ExtensionHost.LogMessage(new LogMessage
            {
                Message = $"Paste skipped: Command Palette still had the foreground after {waited.TotalMilliseconds:F0} ms",
                State = MessageState.Warning,
            });
            return CommandResult.ShowToast(Properties.Resources.copied_toast_text);
        }

        ExtensionHost.LogMessage(new LogMessage { Message = $"Foreground left Command Palette after {waited.TotalMilliseconds:F0} ms" });
        ClipboardHelper.SendPasteKeyCombination();

        if (!_settings.KeepAfterPaste)
        {
            Clipboard.DeleteItemFromHistory(_clipboardItem.Item);
        }

        return CommandResult.ShowToast(Properties.Resources.paste_toast_text);
    }
}
