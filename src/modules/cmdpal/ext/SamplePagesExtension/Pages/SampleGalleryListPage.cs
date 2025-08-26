// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleGalleryListPage : ListPage
{
    public SampleGalleryListPage()
    {
        Icon = new IconInfo("\uE7C5");
        Name = "Sample Gallery List Page";
        GridProperties = new GalleryGridLayout();
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "Sample Title",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Another Title",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "More Titles",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Stop With The Titles",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Another Title",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Space.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "More Titles",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Stop With The Titles",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
            },
        ];
    }
}
