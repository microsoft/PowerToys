// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>
/// Lists Text Expander's snippets, searched by trigger and by text. The snippets file is checked each time the list is
/// asked for, so changes made in the snippet editor show the next time the page opens.
/// </summary>
internal sealed partial class SnippetsPage : DynamicListPage
{
    internal const string PageId = "com.microsoft.cmdpal.builtin.textexpander.snippets";

    private readonly SnippetActions _actions;
    private readonly Lock _lock = new();
    private readonly EmptyStateItem _emptyItem = new();

    // Guarded by _lock. The items are made again when the snippets change, or a minute later, so the dates and
    // times in their previews stay current.
    private Dictionary<Snippet, SnippetListItem> _items = new();
    private SnippetLibrarySnapshot? _itemsFrom;
    private long _itemsMinute;

    public SnippetsPage(SnippetActions actions, string search = "")
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions = actions;
        Icon = Icons.TextExpander;
        Title = Resources.snippets_title;
        Name = Resources.snippets_page_name;
        Id = PageId;
        PlaceholderText = Resources.snippets_placeholder;
        ShowDetails = true;
        if (!string.IsNullOrWhiteSpace(search))
        {
            SetSearchNoUpdate(search);
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    /// <summary>Gets what the page shows when it lists nothing, as of now (see <see cref="EmptyStateItem"/>).</summary>
    public override ICommandItem? EmptyContent => _emptyItem.For(EmptyState.For(_actions.Source.GetState()), out _);

    public override IListItem[] GetItems()
    {
        var state = _actions.Source.GetState();
        var library = state.Library;
        IReadOnlyList<Snippet> snippets = state.IsOn && library.Status == SnippetLibraryStatus.Ready
            ? SnippetSearch.Filter(library.Snippets, SearchText)
            : [];

        IListItem[] result;
        lock (_lock)
        {
            var items = ItemsFor(library);
            result = new IListItem[snippets.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = items[snippets[i]];
            }
        }

        // Changed since Command Palette asked, such as Text Expander turned off while the page is open.
        _ = _emptyItem.For(EmptyState.For(state), out var changed);
        if (changed)
        {
            OnPropertyChanged(nameof(EmptyContent));
        }

        return result;
    }

    private Dictionary<Snippet, SnippetListItem> ItemsFor(SnippetLibrarySnapshot library)
    {
        var minute = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
        if (!ReferenceEquals(library, _itemsFrom) || minute != _itemsMinute)
        {
            var items = new Dictionary<Snippet, SnippetListItem>(library.Snippets.Count);
            foreach (var snippet in library.Snippets)
            {
                items[snippet] = new SnippetListItem(snippet, _actions);
            }

            _items = items;
            _itemsFrom = library;
            _itemsMinute = minute;
        }

        return _items;
    }
}
