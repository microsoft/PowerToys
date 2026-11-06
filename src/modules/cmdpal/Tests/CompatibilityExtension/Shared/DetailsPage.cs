// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class DetailsPage : ListPage
{
    private readonly IListItem[] _items;

    public DetailsPage()
    {
        Id = "compat.details";
        Name = "Open";
        Title = "Details";
        ShowDetails = true;
        Icon = new IconInfo("\uE8A5");
        _items =
        [
            new ListItem(new NoOpCommand())
            {
                Title = "Alpha - rich details",
                Subtitle = "Heading, body, metadata and tags",
                Tags = [new Tag("Alpha")],
                Details = new Details
                {
                    Title = "Alpha details",
                    Body = "## Alpha\nThis is **bold**, *italic*, and `inline code`.\n\n- First bullet\n- Second bullet",
                    Metadata =
                    [
                        new DetailsElement { Key = "SDK", Data = new DetailsLink { Text = Baseline.SdkVersion } },
                        new DetailsElement { Key = "Tags", Data = new DetailsTags { Tags = [new Tag("First"), new Tag("Second")] } },
                    ],
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Beta - different details",
                Details = new Details { Title = "Beta details", Body = "Only Beta content should be visible. Alpha metadata must disappear." },
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Gamma - no details",
                Subtitle = "The details pane must not retain Alpha or Beta content",
            },
        ];
    }

    public override IListItem[] GetItems() => _items;
}
