// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace SamplePagesExtension;

internal sealed partial class GridSamplePage : IListPage
{
    private readonly List<ListItem> _items = new();

    public IIconInfo Icon => new IconInfo("\uE9F9"); // Grid view icon

    public string Title => "Grid Sample";

    public string PlaceholderText => "Search items in grid view";

    public ICommandItem EmptyContent => new CommandItem() 
    { 
        Icon = new IconInfo("\uE9F9"), 
        Title = "No items to display", 
        Subtitle = "This is an empty grid page", 
    };

    public IFilters Filters => null;

    // This makes it display as a grid with 100x100 tiles
    public IGridProperties GridProperties => new GridProperties() { TileSize = new Size(100, 100) };

    public bool HasMoreItems => false;

    public string SearchText => string.Empty;

    public bool ShowDetails => false;

    public OptionalColor AccentColor => default;

    public bool IsLoading => false;

    public string Id => "GridSample";

    public string Name => "Grid Sample";

#pragma warning disable CS0067 // The event is never used
    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged;
    
    public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged;
#pragma warning restore CS0067 // The event is never used

    public GridSamplePage()
    {
        // Create some sample items for the grid
        for (var i = 1; i <= 20; i++)
        {
            var item = new ListItem(new NoOpCommand())
            {
                Title = $"Item {i}",
                Subtitle = $"Description for item {i}",
                Icon = new IconInfo("\uE8A9"), // Placeholder icon
            };
            _items.Add(item);
        }
    }

    public IListItem[] GetItems() => _items.ToArray();

    public void LoadMore()
    {
    }
}
