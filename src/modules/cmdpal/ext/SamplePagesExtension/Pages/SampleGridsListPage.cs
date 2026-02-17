// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleGridsListPage : ListPage
{
    private readonly IListItem[] _items =
    [
        new ListItem(new SampleGalleryListPage { GridProperties = new GalleryGridLayout { ShowTitle = true, ShowSubtitle = true } })
        {
            Title = "Gallery list page (title and subtitle)",
            Subtitle = "A sample gallery list page with images",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
        },
        new ListItem(new SampleGalleryListPage { GridProperties = new GalleryGridLayout { ShowTitle = true, ShowSubtitle = false } })
        {
            Title = "Gallery list page (title, no subtitle)",
            Subtitle = "A sample gallery list page with images",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
        },
        new ListItem(new SampleGalleryListPage { GridProperties = new GalleryGridLayout { ShowTitle = false, ShowSubtitle = false } })
        {
            Title = "Gallery list page (no title, no subtitle)",
            Subtitle = "A sample gallery list page with images",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
        },
        new ListItem(new SampleGalleryListPage { GridProperties = new SmallGridLayout() })
        {
            Title = "Small grid list page",
            Subtitle = "A sample grid list page with text items",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
        },
        new ListItem(new SampleGalleryListPage { GridProperties = new MediumGridLayout { ShowTitle = true } })
        {
            Title = "Medium grid (with title)",
            Subtitle = "A sample grid list page with text items",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
        },
        new ListItem(new SampleGalleryListPage { GridProperties = new MediumGridLayout { ShowTitle = false } })
        {
            Title = "Medium grid (hidden title)",
            Subtitle = "A sample grid list page with text items",
            Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
        }
    ];

    public SampleGridsListPage()
    {
        Icon = new IconInfo("\uE7C5");
        Name = "Grid and gallery lists";
    }

    public override IListItem[] GetItems() => _items;
}
