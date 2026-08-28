// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

// Used by both a content page and Details to exercise the same rendering path.
internal static class SampleComposedContent
{
    public static IContent[] Create()
    {
        var header = new HeaderContent
        {
            Title = "Composed content",
            Subtitle = "An optional subtitle under the header",
            Image = new IconInfo("\uE8A5"),
        };
        var section = new SectionContent
        {
            Title = "Expandable section",
            PreviewItemCount = 2,
            Content =
            [
                new MarkdownContent("The first two direct children are shown initially."),
                new PropertyGridContent
                {
                    Properties =
                    [
                        new PropertyContent { Label = "Kind", Value = new TextContent("Document") },
                        new PropertyContent
                        {
                            Label = "Website",
                            Value = new LinkContent { Link = new Uri("https://github.com/microsoft/PowerToys") },
                        },
                        new PropertyContent
                        {
                            Label = "Tags",
                            Value = new TagsContent
                            {
                                Tags = [new Tag("Sample") { ToolTip = "Tag tooltip", Icon = new IconInfo("\uE8EC") }, new Tag("Content")],
                            },
                        },
                    ],
                },
                new TreeContent
                {
                    RootContent = new MarkdownContent("**Nested tree content**"),
                    Children =
                    [
                        new TextContent("A child rendered inside the section"),
                        new SectionContent
                        {
                            Title = "Nested section",
                            PreviewItemCount = 0,
                            Content = [new MarkdownContent("This starts completely collapsed.")],
                        },
                    ],
                },
                new LinkContent { Text = "Open the repository", Link = new Uri("https://github.com/microsoft/PowerToys") },
                new SeparatorContent(),
                new MarkdownContent("Last section item."),
            ],
        };

        var append = new AnonymousCommand(() =>
        {
            header.Subtitle = "Content updated; the existing section keeps its expansion state.";
            section.Content = [.. section.Content, new TextContent("An appended item")];
        })
        {
            Name = "Append section item",
            Icon = new IconInfo("\uE710"),
            Result = CommandResult.KeepOpen(),
        };
        var toggleSubtitle = new AnonymousCommand(() =>
        {
            header.Subtitle = string.IsNullOrEmpty(header.Subtitle) ? "The subtitle is visible again." : string.Empty;
        })
        {
            Name = "Toggle subtitle",
            Result = CommandResult.KeepOpen(),
        };

        return
        [
            header,
            new MarkdownContent("Headers, properties, links, tags, commands and separators all implement `IContent`."),
            section,
            new SectionContent
            {
                // No title: render a divider followed by the children.
                Content =
                [
                    new SeparatorContent { Title = "Actions" },
                    new CommandsContent { Commands = [append, toggleSubtitle] },
                ],
            },
            new SectionContent
            {
                Title = "No overflow",
                PreviewItemCount = 3,
                Content = [new MarkdownContent("No expansion button is needed here.")],
            },
        ];
    }
}
