// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>
/// Asks which match goes in when a snippet takes part of the copy and the copy has several, as Text Expander's window
/// does: "Pick the email address to insert", above the matches. Typing filters them, and Enter on the last pick pastes.
/// Each spot that asks gets a page, in the order Text Expander asks them. When nothing needs picking, the page holds
/// the snippet as it will go in, and Enter pastes it. Esc goes back without pasting, as cancelling the window does.
/// </summary>
/// <remarks>
/// Command Palette opens a page from an item, not from a command's result, so a snippet that takes part of the copy
/// always opens this page. The first page reads the clipboard each time it's opened, after any questions, as Text
/// Expander reads it once they're answered. Later pages use that copy.
/// </remarks>
internal sealed partial class SnippetPicksPage : ListPage
{
    private readonly Snippet _snippet;
    private readonly SnippetActions _actions;
    private readonly Func<IReadOnlyList<string>>? _answers;
    private readonly Action? _inserted;
    private readonly SnippetFill? _fill;
    private readonly EmptyStateItem _emptyItem = new();

    /// <summary>The first page, which reads the clipboard when it's opened.</summary>
    /// <param name="answers">Gets the answers to the snippet's questions, in order.</param>
    /// <param name="inserted">Runs once the snippet is in, so answers can be cleared.</param>
    public SnippetPicksPage(Snippet snippet, SnippetActions actions, Func<IReadOnlyList<string>>? answers = null, Action? inserted = null)
        : this(snippet, actions, inserted, fill: null)
    {
        _answers = answers;
    }

    /// <summary>A later page, for the next choice, with the copy the first page read and the matches picked so far.</summary>
    private SnippetPicksPage(Snippet snippet, SnippetActions actions, Action? inserted, SnippetFill? fill)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(actions);

        _snippet = snippet;
        _actions = actions;
        _inserted = inserted;
        _fill = fill;
        Name = Resources.paste_command_name;
        Title = snippet.Trigger;
        Icon = Icons.TextExpander;
        if (fill is not null)
        {
            PlaceholderText = ClipboardVariableText.PickTitle(fill.Plan.Choices[fill.Picks.Count].Part);
        }
    }

    /// <summary>Gets what the page shows when Text Expander is off, as of now (see <see cref="EmptyStateItem"/>).</summary>
    public override ICommandItem? EmptyContent
    {
        get
        {
            var state = _actions.Source.GetState();
            return state.IsOn ? null : _emptyItem.For(EmptyState.For(state), out _);
        }
    }

    public override IListItem[] GetItems()
    {
        // Off, or turned off by policy since the page was opened: the clipboard isn't read, nor what was read shown.
        var state = _actions.Source.GetState();
        if (!state.IsOn)
        {
            PlaceholderText = string.Empty;
            _ = _emptyItem.For(EmptyState.For(state), out var changed);
            if (changed)
            {
                OnPropertyChanged(nameof(EmptyContent));
            }

            return [];
        }

        var fill = _fill ?? ReadClipboard();
        if (fill.Picks.Count >= fill.Plan.Choices.Count)
        {
            PlaceholderText = Resources.picks_nothing_to_pick;
            return [WhatGoesIn(fill)];
        }

        var choice = fill.Plan.Choices[fill.Picks.Count];
        PlaceholderText = ClipboardVariableText.PickTitle(choice.Part);
        var items = new IListItem[choice.Options.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = MatchItem(fill, choice, choice.Options[i]);
        }

        return items;
    }

    private SnippetFill ReadClipboard()
    {
        var answers = _answers?.Invoke() ?? [];
        var plan = SnippetText.PlanClipboard(_snippet.Replacement, answers, _actions.Inserter.Clipboard.ReadText());
        return new SnippetFill(answers, plan, []);
    }

    /// <summary>One match to pick. Picking the last choice pastes; an earlier one opens the page for the next.</summary>
    private ListItem MatchItem(SnippetFill fill, ClipboardChoice choice, string match)
    {
        string[] picks = [.. fill.Picks, match];
        var picked = fill with { Picks = picks };
        var last = picked.Picks.Count == picked.Plan.Choices.Count;
        return new ListItem(last ? Insert(picked) : new SnippetPicksPage(_snippet, _actions, _inserted, picked))
        {
            Title = match,
            Subtitle = SnippetText.OneLine(SnippetText.Preview(_snippet.Replacement, picked)),
            Icon = ClipboardVariableText.PartIcon(choice.Part),
            MoreCommands = last ? [new CommandContextItem(_actions.CopyCommand(_snippet, picked, _inserted))] : [],
        };
    }

    /// <summary>The snippet as it will go in, when nothing needs picking, with a word about any spot left empty.</summary>
    private ListItem WhatGoesIn(SnippetFill fill)
    {
        var expanded = SnippetText.ExpandWithGaps(_snippet.Replacement, fill);
        return new ListItem(Insert(fill))
        {
            Title = expanded.Text.Length == 0 ? Resources.picks_comes_out_empty : SnippetText.OneLine(expanded.Text),
            Subtitle = ClipboardVariableText.NothingFound(expanded.Gaps) ?? string.Empty,
            Icon = Icons.TextExpander,
            MoreCommands = [new CommandContextItem(_actions.CopyCommand(_snippet, fill, _inserted))],
        };
    }

    private InsertSnippetCommand Insert(SnippetFill fill) => new(_snippet, _actions.Source, _actions.Inserter, fill, _inserted);
}
