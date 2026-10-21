// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SamplePagesExtension;

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
        IListItem[] sectionList =
        [
            CreateSectionHeader("This is a section list", "1 of 12 items"),
            new ListItem(new NoOpCommand())
            {
                Title = "Sample Title",
                Subtitle = "I don't do anything",
                Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
            },
        ];
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
            CreateSectionHeader("Section with a custom icon", "1 item", new ToastCommand("Open in new window invoked", MessageState.Success)
            {
                Name = "Open in new window",
                Icon = new IconInfo("\uE8A7"),
            }),
            new ListItem(new NoOpCommand())
            {
                Title = "Custom icon example",
                Subtitle = "The section action uses a custom icon",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
            },
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

    private ListItem CreateSectionHeader(string sectionTitle, string subtitle, ICommand sectionCommand = null)
    {
        var header = new ListItem
        {
            Command = null,
            Title = sectionTitle,
            Section = sectionTitle,
            Subtitle = subtitle,
            MoreCommands =
            [
                new CommandContextItem(new ToastCommand($"Refresh invoked for '{sectionTitle}'", MessageState.Success)
                {
                    Name = "Refresh section",
                    Icon = new IconInfo("\uE72C"),
                }),
                new Separator(),
                new CommandContextItem(new ToastCommand($"Showing info for '{sectionTitle}'", MessageState.Info)
                {
                    Name = "Section info",
                    Icon = new IconInfo("\uE946"),
                }),
            ],
        };
        header.GetProperties()[WellKnownExtensionAttributes.SectionCommand] = sectionCommand ?? CreateShowMoreCommand(sectionTitle);
        return header;
    }

    private ICommand CreateShowMoreCommand(string sectionTitle)
    {
        var viewName = GridProperties switch
        {
            GalleryGridLayout => "gallery",
            not null => "grid",
            _ => "list",
        };

        return new ToastCommand($"Show more invoked for '{sectionTitle}' in the {viewName} view", MessageState.Success)
        {
            Name = "Show more...",
            Icon = new IconInfo("\uE76C"),
        };
    }
}
