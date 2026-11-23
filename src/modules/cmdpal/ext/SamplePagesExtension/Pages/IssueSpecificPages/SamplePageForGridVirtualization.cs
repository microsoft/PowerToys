// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages.IssueSpecificPages;

internal sealed partial class SamplePageForGridVirtualization : ListPage
{
    private const int InitialTileCount = 10000;
    private const int AdditionalTileCount = 1000;

    private IListItem[] _items = [];

    public SamplePageForGridVirtualization()
    {
        Icon = new IconInfo("\uE80A");
        Name = "Grid virtualization and recycling";
        Title = Name;
        GridProperties = new GalleryGridLayout();
        EmptyContent = new CommandItem(new NoOpCommand()) { Title = "No matching grid items" };
        HasMoreItems = true;
    }

    public override IListItem[] GetItems()
    {
        if (_items.Length == 0)
        {
            _items = CreateInitialItems();
        }

        return _items;
    }

    public override void LoadMore()
    {
        var items = new List<IListItem>(GetItems());
        for (var i = 0; i < AdditionalTileCount; i++)
        {
            items.Add(new ListItem(new NoOpCommand { Id = $"grid-sample-appended-{i}", Name = string.Empty })
            {
                Title = $"Appended tile {i:D4}",
                Icon = Icon,
            });
        }

        _items = [.. items];
        HasMoreItems = false;
        RaiseItemsChanged(_items.Length);
    }

    private static IListItem[] CreateInitialItems()
    {
        List<IListItem> items = [];
        IconInfo[] icons =
        [
            new("\uE80A"),
            new("\uE8A5"),
            new("\uE713"),
            new("\uE8B7"),
            IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
            IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
            IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
        ];
        var repeatedHeader = new Separator("Repeated section");

        for (var i = 0; i < InitialTileCount; i++)
        {
            if (i % 137 == 7)
            {
                if (i % 411 == 7)
                {
                    items.Add(new Separator());
                    items.Add(repeatedHeader);
                }

                items.Add(new Separator($"Section {i / 137}"));
            }

            items.Add(new ListItem(new NoOpCommand { Id = $"grid-sample-{i}", Name = string.Empty })
            {
                Title = i % 7 == 0 ? string.Empty : $"Tile {i:D5}",
                Subtitle = i % 3 == 0 ? string.Empty : $"Subtitle {i:D5}",
                Icon = icons[i % icons.Length],
                MoreCommands = [new CommandContextItem(new NoOpCommand { Name = $"Inspect tile {i:D5}" })],
            });
        }

        items.Add(new Separator("Trailing section"));
        return [.. items];
    }
}
