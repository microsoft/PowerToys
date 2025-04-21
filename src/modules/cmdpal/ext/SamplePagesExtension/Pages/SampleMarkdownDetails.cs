// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleMarkdownDetails : ContentPage
{
    public SampleMarkdownDetails()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Markdown with Details";

        Details = new Details()
        {
            Body = "... with _even more Markdown_ by it.\nEach of the sections below is some sample metadata",
            Metadata = [
                new DetailsElement()
                        {
                            Key = "Plain text",
                            Data = new DetailsLink() { Text = "Set just the text to get text metadata" },
                        },
                        new DetailsElement()
                        {
                            Key = "Links",
                            Data = new DetailsLink() { Text = "Or metadata can be links", Link = new("https://github.com/microsoft/PowerToys") },
                        },
                        new DetailsElement()
                        {
                            Key = "CmdPal will display the URL if no text is given",
                            Data = new DetailsLink() { Link = new("https://github.com/microsoft/PowerToys") },
                        },
                        new DetailsElement()
                        {
                            Key = "Above a separator",
                            Data = new DetailsLink() { Text = "Below me is a separator" },
                        },
                        new DetailsElement()
                        {
                            Key = "A separator",
                            Data = new DetailsSeparator(),
                        },
                        new DetailsElement()
                        {
                            Key = "Below a separator",
                            Data = new DetailsLink() { Text = "Above me is a separator" },
                        },
                        new DetailsElement()
                        {
                            Key = "Add Tags too",
                            Data = new DetailsTags()
                            {
                                Tags = [
                                    new Tag("simple text"),
                                    new Tag("Colored text") { Foreground = ColorHelpers.FromRgb(255, 0, 0) },
                                    new Tag("Colored backgrounds") { Background = ColorHelpers.FromRgb(0, 0, 255) },
                                    new Tag("Colored everything") { Foreground = ColorHelpers.FromRgb(255, 255, 0), Background = ColorHelpers.FromRgb(0, 0, 255) },
                                    new Tag("Icons too") { Icon = new IconInfo("\uE735"), Foreground = ColorHelpers.FromRgb(255, 255, 0) },
                                    new Tag() { Icon = new IconInfo("https://i.imgur.com/t9qgDTM.png") },
                                    new Tag("this") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("baby") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("can") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("fit") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("so") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("many") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("tags") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("in") { Foreground = RandomColor(), Background = RandomColor() },
                                    new Tag("it") { Foreground = RandomColor(), Background = RandomColor() },
                                ],
                            },
                        },
                    ],
        };
    }

    public override IContent[] GetContent() => [new MarkdownContent(
"""
# This page also has details

So you can have markdown...
"""),
new MarkdownContent("""
But what this is really useful for is the tags and other things you can put into
Details. Which I'd do. **IF I HAD ANY**.
""")
    ];

    private static OptionalColor RandomColor()
    {
        var r = new Random();
        var b = new byte[3];
        r.NextBytes(b);
        return ColorHelpers.FromRgb(b[0], b[1], b[2]);
    }
}
