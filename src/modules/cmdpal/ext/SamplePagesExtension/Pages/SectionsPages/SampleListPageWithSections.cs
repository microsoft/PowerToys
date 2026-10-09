// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages.SectionsPages;

internal sealed partial class SampleListPageWithSections : ListPage
{
    public SampleListPageWithSections()
    {
        Icon = new IconInfo("\uE7C5");
        Name = "Sample Gallery List Page";
    }

    public SampleListPageWithSections(IGridProperties gridProperties)
    {
        Icon = new IconInfo("\uE7C5");
        Name = "Sample Gallery List Page";
        GridProperties = gridProperties;
    }

    public override IListItem[] GetItems()
    {
        var sectionList = new Section("This is a section list", [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "Sample Title",
                        Subtitle = "I don't do anything",
                        Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
                    },
                ]);
        var anotherSectionList = new Section("This is another section list", [
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
                ]);

        var yesTheresAnother = new Section("There's another", [
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
            ]);

        return [
            ..sectionList,
            ..anotherSectionList,
            new Separator(),
            new ListItem(new NoOpCommand())
            {
                Title = "Separators also work",
                Subtitle = "But I still don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
            },
            ..yesTheresAnother
        ];
    }
}
