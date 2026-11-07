// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander;

/// <summary>Makes the commands for a snippet, the same on the snippets page and in the main search.</summary>
internal sealed class SnippetActions
{
    public SnippetActions(SnippetSource source, SnippetInserter inserter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(inserter);

        Source = source;
        Inserter = inserter;
    }

    public SnippetSource Source { get; }

    public SnippetInserter Inserter { get; }

    /// <summary>
    /// Gets what Enter does: paste; ask the snippet's questions first; open its pick page when it takes part of the copy,
    /// which may have several matches; or explain that only Text Expander can insert it.
    /// </summary>
    public ICommand PrimaryCommand(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        return snippet.Kind switch
        {
            SnippetKind.Ready when snippet.UsesClipboardParts => new SnippetPicksPage(snippet, this),
            SnippetKind.Ready => new InsertSnippetCommand(snippet, Source, Inserter),
            SnippetKind.AsksQuestions => new SnippetAnswersPage(snippet, this),
            _ => new TextExpanderOnlyCommand(),
        };
    }

    /// <summary>
    /// Gets the other commands. A snippet with questions offers Copy once they're answered, and one that takes part of
    /// the copy once it's picked.
    /// </summary>
    public IContextItem[] MoreCommands(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        return snippet.Kind == SnippetKind.Ready && !snippet.UsesClipboardParts ? [new CommandContextItem(CopyCommand(snippet, answers: null))] : [];
    }

    /// <summary>Copies a snippet, with its answers; <paramref name="copied"/> runs once it's copied, so they can be cleared.</summary>
    public CopySnippetCommand CopyCommand(Snippet snippet, Func<IReadOnlyList<string>>? answers, Action? copied = null)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        return new CopySnippetCommand(
            () => Source.GetState().IsOn ? SnippetText.Expand(snippet.Replacement, answers?.Invoke() ?? [], Inserter.Clipboard.ReadText) : null,
            Inserter.Clipboard,
            copied);
    }

    /// <summary>Copies a snippet with what its pick pages gathered, as <see cref="InsertSnippetCommand"/> would paste it.</summary>
    public CopySnippetCommand CopyCommand(Snippet snippet, SnippetFill fill, Action? copied = null)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(fill);

        return new CopySnippetCommand(() => Source.GetState().IsOn ? SnippetText.Expand(snippet.Replacement, fill) : null, Inserter.Clipboard, copied);
    }

    public ITag[] Tags(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        return snippet.Kind switch
        {
            SnippetKind.AsksQuestions => [new Tag(Resources.tag_asks_questions)],
            SnippetKind.TextExpanderOnly => [new Tag(Resources.tag_text_expander_only)],
            _ => [],
        };
    }
}
