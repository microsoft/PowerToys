// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Commands;

/// <summary>
/// Pastes a snippet into the app Command Palette was opened over: Command Palette hides, the snippet is expanded with
/// any answers given, and <see cref="SnippetInserter"/> puts it in.
/// </summary>
internal sealed partial class InsertSnippetCommand : InvokableCommand
{
    private readonly Snippet _snippet;
    private readonly SnippetSource _source;
    private readonly SnippetInserter _inserter;
    private readonly Func<IReadOnlyList<string>>? _answers;
    private readonly Action? _inserted;
    private readonly SnippetFill? _fill;

    /// <param name="answers">Gets the answers to the snippet's questions, in order.</param>
    /// <param name="inserted">Runs once the snippet is in, so answers can be cleared.</param>
    internal InsertSnippetCommand(Snippet snippet, SnippetSource source, SnippetInserter inserter, Func<IReadOnlyList<string>>? answers = null, Action? inserted = null)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(inserter);

        _snippet = snippet;
        _source = source;
        _inserter = inserter;
        _answers = answers;
        _inserted = inserted;
        Name = Resources.paste_command_name;
        Icon = Icons.Paste;
    }

    /// <param name="fill">What the snippet's pick pages gathered: the answers, the copy they read, and the matches picked.</param>
    /// <param name="inserted">Runs once the snippet is in, so answers can be cleared.</param>
    internal InsertSnippetCommand(Snippet snippet, SnippetSource source, SnippetInserter inserter, SnippetFill fill, Action? inserted = null)
        : this(snippet, source, inserter, answers: null, inserted)
    {
        ArgumentNullException.ThrowIfNull(fill);

        _fill = fill;
    }

    public override ICommandResult Invoke()
    {
        var state = _source.GetState();
        if (!state.IsOn)
        {
            return KeepOpenToast(Resources.toast_off);
        }

        // Clipboard variables read the clipboard now, before the paste borrows it, unless the pick pages already have.
        var expanded = _fill is null
            ? SnippetText.ExpandWithGaps(_snippet.Replacement, _answers?.Invoke() ?? [], _inserter.Clipboard.ReadText)
            : SnippetText.ExpandWithGaps(_snippet.Replacement, _fill);
        var text = expanded.Text;
        var outcome = _inserter.Insert(text, state.Settings.AlwaysType);
        if (outcome is InsertOutcome.Inserted or InsertOutcome.Empty or InsertOutcome.ClipboardNotRestored)
        {
            _inserted?.Invoke();
        }

        return outcome switch
        {
            // A spot the copy had nothing for was left empty: say so, as Text Expander does.
            InsertOutcome.Inserted or InsertOutcome.Empty => ClipboardVariableText.NothingFound(expanded.Gaps) is { } nothingFound
                ? CommandResult.ShowToast(nothingFound)
                : CommandResult.Dismiss(),
            InsertOutcome.ClipboardNotRestored => CommandResult.ShowToast(Resources.toast_clipboard_not_restored),

            // The snippet didn't go in: the page stays as it was, and the toast offers to copy it instead.
            _ => CommandResult.ShowToast(new ToastArgs
            {
                Message = MessageFor(outcome),
                Result = CommandResult.KeepOpen(),
                Command = new CopySnippetCommand(() => text, _inserter.Clipboard),
            }),
        };
    }

    internal static string MessageFor(InsertOutcome outcome) => outcome switch
    {
        InsertOutcome.NoTarget => Resources.toast_no_target,
        InsertOutcome.TargetOutOfReach => Resources.toast_out_of_reach,
        InsertOutcome.TargetChanged => Resources.toast_target_changed,
        InsertOutcome.ClipboardNotRestored => Resources.toast_clipboard_not_restored,
        _ => Resources.toast_failed,
    };

    private static CommandResult KeepOpenToast(string message) => CommandResult.ShowToast(new ToastArgs
    {
        Message = message,
        Result = CommandResult.KeepOpen(),
    });
}
