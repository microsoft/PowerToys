// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SlowListPage : ListPage
{
    public SlowListPage()
    {
        Icon = new IconInfo("\uEA79");
        Name = "Slow List Page";
        Title = "This page simulates a slow load";
    }

    public override IListItem[] GetItems()
    {
        Thread.Sleep(5000);

        return [
            new ListItem(new NoOpCommand())
            {
                Title = "This is a basic item in the list",
                Subtitle = "I don't do anything though",
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This is another item in the list",
                Subtitle = "Still nothing",
            },
        ];
    }
}
