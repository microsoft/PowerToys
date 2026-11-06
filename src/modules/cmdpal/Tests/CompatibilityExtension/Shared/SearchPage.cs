// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class SearchPage : DynamicListPage
{
    private static readonly (string Name, string Group)[] Entries =
    [
        ("Apple", "fruit"),
        ("Apricot", "fruit"),
        ("Banana", "fruit"),
        ("Carrot", "vegetable"),
        ("Potato", "vegetable"),
    ];

    private readonly SearchFilters _filters = new();

    public SearchPage()
    {
        Id = "compat.search";
        Name = "Open";
        Title = "Search and filters";
        PlaceholderText = "Type app, then zzz, then clear the search";
        Icon = new IconInfo("\uE721");
        Filters = _filters;
        _filters.PropChanged += OnFilterChanged;
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = "No matching fixture items",
            Subtitle = "Clear the query and choose All to restore five items",
            Icon = new IconInfo("\uE721"),
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems() => Entries
        .Where(entry => (_filters.CurrentFilterId == "all" || entry.Group == _filters.CurrentFilterId)
            && entry.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        .Select(entry => (IListItem)new ListItem(new NoOpCommand())
        {
            Title = entry.Name,
            Subtitle = entry.Group,
            Icon = new IconInfo("\uE8B7"),
        })
        .ToArray();

    private void OnFilterChanged(object sender, IPropChangedEventArgs args) => RaiseItemsChanged();
}
