// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed partial class SampleListPageWithDetails : ListPage
{
    public SampleListPageWithDetails()
    {
        Icon = new(string.Empty);
        Name = "Sample List Page with Details";
        this.ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "This page demonstrates Details on ListItems",
                Details = new Details()
                {
                    Title = "List Item 1",
                    Body = "Each of these items can have a `Body` formatted with **Markdown**",
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This one has a subtitle too",
                Subtitle = "Example Subtitle",
                Details = new Details()
                {
                    Title = "List Item 2",
                    Body = SampleMarkdownPage.SampleMarkdownText,
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This one has a tag too",
                Subtitle = "the one with a tag",
                Tags = [
                    new Tag()
                    {
                        Text = "Sample Tag",
                    }
                ],
                Details = new Details()
                {
                    Title = "List Item 3",
                    Body = "### Example of markdown details",
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This one has a hero image",
                Tags = [],
                Details = new Details()
                {
                    Title = "Hero Image Example",
                    HeroImage = new("https://m.media-amazon.com/images/M/MV5BNDBkMzVmNGQtYTM2OC00OWRjLTk5OWMtNzNkMDI4NjFjNTZmXkEyXkFqcGdeQXZ3ZXNsZXk@._V1_QL75_UX500_CR0,0,500,281_.jpg"),
                    Body = "It is literally an image of a hero",
                },
            }
        ];
    }
}
