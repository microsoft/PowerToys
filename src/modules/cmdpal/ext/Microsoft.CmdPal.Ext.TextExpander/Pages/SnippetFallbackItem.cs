// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>
/// Offers the snippet whose trigger best matches what's typed in Command Palette's main search, so a snippet can be
/// pasted without opening the snippets page first. Its other commands include the page, opened on the same search.
/// Nothing shows while Text Expander is off.
/// </summary>
internal sealed partial class SnippetFallbackItem : FallbackCommandItem
{
    internal const string FallbackId = "com.microsoft.cmdpal.builtin.textexpander.fallback";

    private readonly SnippetActions _actions;
    private readonly Lock _lock = new();
    private Snippet? _shown;
    private int _queryVersion;

    public SnippetFallbackItem(SnippetActions actions)
        : base(new NoOpCommand(), Resources.fallback_display_title, FallbackId)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions = actions;
        Title = string.Empty;
        Subtitle = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        var queryVersion = Interlocked.Increment(ref _queryVersion);
        Snippet? match = null;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var state = _actions.Source.GetState(SnippetSource.SearchMaxAge);
            if (state.IsOn && state.Library.Status == SnippetLibraryStatus.Ready)
            {
                match = SnippetSearch.BestTriggerMatch(state.Library.Snippets, query);
            }
        }

        lock (_lock)
        {
            // The host can overlap lookups: an older query must not replace a newer result when it finishes later.
            if (queryVersion != Volatile.Read(ref _queryVersion))
            {
                return;
            }

            if (match is null)
            {
                if (_shown is not null || !string.IsNullOrEmpty(Title))
                {
                    _shown = null;
                    Command = new NoOpCommand();
                    Title = string.Empty;
                    Subtitle = string.Empty;
                    Icon = null;
                    MoreCommands = [];
                }

                return;
            }

            if (!ReferenceEquals(match, _shown))
            {
                _shown = match;
                Command = _actions.PrimaryCommand(match);
                Title = match.Trigger;
                Subtitle = SnippetText.OneLine(SnippetText.Preview(match.Replacement));
                Icon = Icons.TextExpander;
            }

            MoreCommands = [.. _actions.MoreCommands(match), ShowAllCommand(query.Trim())];
        }
    }

    private CommandContextItem ShowAllCommand(string search) => new(new SnippetsPage(_actions, search))
    {
        Title = Resources.fallback_show_all_name,
        Icon = Icons.Search,
    };
}
