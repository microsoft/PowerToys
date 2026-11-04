// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Commands;

/// <summary>
/// Copies a snippet's text to the clipboard. The user asked for the copy, so unlike a paste it stays there and isn't
/// kept out of Clipboard History.
/// </summary>
internal sealed partial class CopySnippetCommand : InvokableCommand
{
    private readonly Func<string?> _text;
    private readonly IClipboardAccess _clipboard;

    /// <param name="text">Gets the text to copy, expanded when the copy is made; null when Text Expander is off.</param>
    internal CopySnippetCommand(Func<string?> text, IClipboardAccess clipboard)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(clipboard);

        _text = text;
        _clipboard = clipboard;
        Name = Resources.copy_command_name;
        Icon = Icons.Copy;
    }

    public override ICommandResult Invoke()
    {
        var text = _text();
        if (text is null)
        {
            return CommandResult.ShowToast(new ToastArgs { Message = Resources.toast_off, Result = CommandResult.KeepOpen() });
        }

        return _clipboard.WriteText(text, keepOutOfHistory: false) == ClipboardWrite.Written
            ? CommandResult.ShowToast(Resources.copied_toast)
            : CommandResult.ShowToast(new ToastArgs { Message = Resources.copy_failed_toast, Result = CommandResult.KeepOpen() });
    }
}
