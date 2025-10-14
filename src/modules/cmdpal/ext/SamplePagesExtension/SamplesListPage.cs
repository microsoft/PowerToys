// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SamplePagesExtension.Pages;

namespace SamplePagesExtension;

public partial class SamplesListPage : ListPage
{
    private readonly IListItem[] _commands = [

        // List pages
        new ListItem(new SampleListPage())
        {
            Title = "List Page Sample Command",
            Subtitle = "Display a list of items",
        },
        new ListItem(new SampleListPageWithDetails())
        {
            Title = "List Page With Details",
            Subtitle = "A list of items, each with additional details to display",
        },
        new ListItem(new SampleUpdatingItemsPage())
        {
            Title = "List page with items that change",
            Subtitle = "The items on the list update themselves in real time",
        },
        new ListItem(new SampleDynamicListPage())
        {
            Title = "Dynamic List Page Command",
            Subtitle = "Changes the list of items in response to the typed query",
        },
        new ListItem(new SampleGalleryListPage())
        {
            Title = "Gallery List Page Command",
            Subtitle = "Displays items as a gallery",
        },
        new ListItem(new OnLoadPage())
        {
            Title = "Demo of OnLoad/OnUnload",
            Subtitle = "Changes the list of items every time the page is opened / closed",
        },
        new ListItem(new SampleIconPage())
        {
            Title = "Sample Icon Page",
            Subtitle = "A demo of using icons in various ways",
        },
        new ListItem(new SlowListPage())
        {
            Title = "Slow loading list page",
            Subtitle = "A demo of a list page that takes a while to load",
        },

        // Content pages
        new ListItem(new SampleContentPage())
        {
            Title = "Sample content page",
            Subtitle = "Display mixed forms, markdown, and other types of content",
        },
        new ListItem(new SampleTreeContentPage())
        {
            Title = "Sample nested content",
            Subtitle = "Example of nesting a tree of content",
        },
        new ListItem(new SampleCommentsPage())
        {
            Title = "Sample of nested comments",
            Subtitle = "Demo of using nested trees of content to create a comment thread-like experience",
            Icon = new IconInfo("\uE90A"), // Comment
        },
        new ListItem(new SampleMarkdownPage())
        {
            Title = "Markdown Page Sample Command",
            Subtitle = "Display a page of rendered markdown",
        },
            new ListItem(new SampleMarkdownManyBodies())
        {
            Title = "Markdown with multiple blocks",
            Subtitle = "A page with multiple blocks of rendered markdown",
        },
        new ListItem(new SampleMarkdownDetails())
        {
            Title = "Markdown with details",
            Subtitle = "A page with markdown and details",
        },
        new ListItem(new SampleMarkdownImagesPage())
        {
            Title = "Markdown with images",
            Subtitle = "A page with rendered markdown and images",
            Icon = new IconInfo("\uee71"),
        },

        // Settings helpers
        new ListItem(new SampleSettingsPage())
        {
            Title = "Sample settings page",
            Subtitle = "A demo of the settings helpers",
        },

        // Evil edge cases
        // Anything weird that might break the palette - put that in here.
        new ListItem(new EvilSamplesPage())
        {
            Title = "Evil samples",
            Subtitle = "Samples designed to break the palette in many different evil ways",
        }
    ];

    public SamplesListPage()
    {
        Name = "Samples";
        Icon = new IconInfo("\ue946"); // Info
    }

    public override IListItem[] GetItems() => _commands;
}
