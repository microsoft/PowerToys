// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal partial class SampleListPageWithItems : ListPage
{
    private static readonly IconInfo _itemIcon = new(new FontIconData("\u0024", "Webdings"));

    private readonly IListItem[] _items = BuildItems();

    public SampleListPageWithItems()
    {
        ShowDetails = true;
    }

    private static IListItem[] BuildItems()
    {
        return Enumerable.Range(1, 100).Select(BuildItem).ToArray();

        IListItem BuildItem(int i)
        {
            var item = new ListItem(new NoOpCommand())
            {
                Icon = _itemIcon,
                Title = $"Item {i}",
                Subtitle = $"Subtitle for item {i}",
            };

            // every other block of 10 items, add details
            if (i % 20 > 10)
            {
                item.Title += " (with details)";
                item.Details = new Details
                {
                    Title = $"Details for item {i}",
                    Body = $"This is some more information about item {i}. It only shows up if you select the item and look at the details pane.",

                    // to demonstrate the automatic wrapping behavior, we set the breakpoint to medium,
                    Size = ContentSize.Medium,
                };
            }

            // for every 5th item, add a tag
            if (i % 5 == 0)
            {
                item.Tags = [new Tag($"Tag for item {i}")];
            }

            return item;
        }
    }

    public override IListItem[] GetItems() => _items;
}
