// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;

namespace SamplePagesExtension;

internal sealed partial class SampleListPageWithDetailsNoShow : ListPage
{
    public SampleListPageWithDetailsNoShow()
    {
        Icon = new IconInfo("\uE8A0");
        Name = Title = "Sample List Page with Details (ShowDetails=false)";
        this.ShowDetails = false; // THIS IS THE KEY CHANGE - SET TO FALSE TO TEST THE FEATURE
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "Item with Details - Should show 'Show Details' action",
                Subtitle = "This item has details but ShowDetails=false",
                Details = new Details()
                {
                    Title = "Test Item 1",
                    Body = "This detail should only show when you activate the 'Show Details' context action!",
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Another item with Details",
                Subtitle = "Also has details",
                Details = new Details()
                {
                    Title = "Test Item 2",
                    Body = "Another item with **markdown** details that should be hidden by default.",
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Item WITHOUT Details",
                Subtitle = "This one has no details so should NOT have a 'Show Details' action",

                // No Details property set - this should NOT get the Show Details action
            },
        ];
    }
}
