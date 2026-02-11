// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleSinglelineListPage : ListPage
{
    public SampleSinglelineListPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "Sample Compact List Page";
        GridProperties = new SinglelineListLayout();
    }

    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new NoOpCommand())
            {
                Icon = new IconInfo("\uE8D2"),
                Title = "This is a basic item in the multiline list",
                Subtitle = "It has a subtitle too",
            },
            new ListItem(new NoOpCommand())
            {
                Icon = new IconInfo("\uE8D2"),
                Title = "This is a basic item in the multiline list",
                Subtitle = "It has a subtitle too",
            },
            new ListItem(new NoOpCommand())
            {
                Icon = new IconInfo("\uE8D2"),
                Title = "This is a basic item in the multiline list",
            },
            new ListItem(new NoOpCommand())
            {
                Icon = new IconInfo("\uE8D2"),
                Title = "This is a basic item in the multiline list",
            },
            new ListItem(new NoOpCommand())
            {
                Icon = new IconInfo("\uE8D2"),
                Title = "This is a basic item in the multiline list",
                Subtitle = "It has a subtitle too",
                Tags = [new Tag("Alpha"), new Tag("Beta")],
            },
        ];
    }
}
