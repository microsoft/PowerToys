// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleComposedContentSamplesPage : ListPage
{
    private readonly IListItem[] _items =
    [
        new ListItem(new SnapshotContentPage("Composed content page", SampleComposedContent.Create()))
        {
            Title = "Composed content page",
            Subtitle = "Headers, property grids, links, tags, commands and nested sections",
        },
        CreateDetailsItem(ContentSize.Small),
        CreateDetailsItem(ContentSize.Medium),
        CreateDetailsItem(ContentSize.Large),
        CreateImagePreviewItem(),
        new ListItem(new SnapshotContentPage("Section preview counts", CreateSectionSamples()))
        {
            Title = "Section preview counts",
            Subtitle = "All, zero, partial, no-overflow and empty-section cases",
        },
        new ListItem(new SampleLazyDetailsPage())
        {
            Title = "Lazy details loading",
            Subtitle = "Immediate previews, delayed snapshots, cached results and retry",
        },
    ];

    public SampleComposedContentSamplesPage()
    {
        Name = "Open";
        Title = "Composed content samples";
        Icon = new IconInfo("\uE8A5");
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => _items;

    private static ListItem CreateDetailsItem(ContentSize size) => new(new NoOpCommand())
    {
        Title = $"Composed details ({size})",
        Subtitle = "Expand a section, append an item, and toggle the subtitle",
        Details = new ContentDetails { Size = size, Content = SampleComposedContent.Create() },
    };

    private static ListItem CreateImagePreviewItem() => new(new NoOpCommand())
    {
        Title = "Image preview details",
        Subtitle = "Image viewer first, then header and compact properties",
        Details = new ContentDetails
        {
            Size = ContentSize.Large,
            Content =
            [
                new ImageContent(IconHelpers.FromRelativePath("Assets/Images/win-11-bloom-6k.jpg"))
                {
                    MaxWidth = 640,
                    MaxHeight = 240,
                },
                new HeaderContent
                {
                    Image = new IconInfo("\uE91B"),
                    Title = "Windows bloom wallpaper",
                    Subtitle = "The image preview precedes this header",
                },
                new PropertyGridContent
                {
                    Properties =
                    [
                        new PropertyContent { Label = "File name", Value = new TextContent("win-11-bloom-6k.jpg") },
                        new PropertyContent { Label = "Dimensions", Value = new TextContent("4500 x 3000 pixels") },
                        new PropertyContent { Label = "Format", Value = new TextContent("JPEG") },
                        new PropertyContent { Label = "Source", Value = new TextContent("Bundled sample image; no network request") },
                    ],
                },
            ],
        },
    };

    private static IContent[] CreateSectionSamples() =>
    [
        new HeaderContent
        {
            Title = "Section preview counts",
            Subtitle = "PreviewItemCount counts direct children. Expansion does not fetch data.",
        },
        new SectionContent { Title = "Default: all children", Content = Rows() },
        new SectionContent { Title = "Zero: heading only", PreviewItemCount = 0, Content = Rows() },
        new SectionContent { Title = "Two: partial preview", PreviewItemCount = 2, Content = Rows() },
        new SectionContent { Title = "Ten: no overflow button", PreviewItemCount = 10, Content = Rows() },
        new SectionContent { Title = "Empty section: no overflow button", PreviewItemCount = 0 },
        new SectionContent { Content = [new MarkdownContent("An unnamed section starts with a divider.")] },
    ];

    private static IContent[] Rows() =>
    [
        new MarkdownContent("First child"),
        new MarkdownContent("Second child"),
        new MarkdownContent("Third child"),
    ];

    private sealed partial class SnapshotContentPage : ContentPage
    {
        private readonly IContent[] _content;

        public SnapshotContentPage(string title, IContent[] content)
        {
            Name = "Open";
            Title = title;
            Icon = new IconInfo("\uE8A5");
            _content = content;
        }

        public override IContent[] GetContent() => _content;
    }
}
