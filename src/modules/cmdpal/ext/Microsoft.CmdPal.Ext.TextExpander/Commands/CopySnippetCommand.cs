// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Commands;

/// <summary>
/// Copies a snippet's text to the clipboard. The user asked for the copy, so unlike a paste it stays there and isn't
/// kept out of Clipboard History.
/// </summary>
internal sealed partial class CopySnippetCommand : InvokableCommand
{
    private readonly Func<string?> _text;
    private readonly IClipboardAccess _clipboard;
    private readonly Action? _copied;

    /// <param name="text">Gets the text to copy, expanded when the copy is made; null when Text Expander is off.</param>
    /// <param name="clipboard">The clipboard.</param>
    /// <param name="copied">Runs once the copy is made, so a snippet's answers can be cleared, as after a paste.</param>
    internal CopySnippetCommand(Func<string?> text, IClipboardAccess clipboard, Action? copied = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(clipboard);

        _text = text;
        _clipboard = clipboard;
        _copied = copied;
        Name = Resources.copy_command_name;
        Icon = Icons.Copy;
    }

    public override ICommandResult Invoke()
    {
        var text = _text();
        if (text is null)
        {
            return KeepOpenToast(Resources.toast_off);
        }

        if (text.Length == 0)
        {
            return KeepOpenToast(Resources.picks_comes_out_empty);
        }

        // With CR LF line breaks, as the clipboard has text: the console runs pasted lines with a lone LF as one.
        var write = _clipboard.WriteText(ClipboardFormats.WithClipboardLineBreaks(text), keepOutOfHistory: false);
        if (write != ClipboardWrite.Written)
        {
            return KeepOpenToast(write == ClipboardWrite.Failed
                ? string.Concat(Resources.copy_failed_toast, " ", Resources.toast_clipboard_not_restored)
                : Resources.copy_failed_toast);
        }

        _copied?.Invoke();
        return CommandResult.ShowToast(Resources.copied_toast);
    }

    private static CommandResult KeepOpenToast(string message) => CommandResult.ShowToast(new ToastArgs
    {
        Message = message,
        Result = CommandResult.KeepOpen(),
    });
}
