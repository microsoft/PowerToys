// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Timers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualBasic;

namespace SamplePagesExtension;

public partial class SampleUpdatingItemsPage : ListPage
{
    private readonly ListItem hourItem = new(new NoOpCommand());
    private readonly ListItem minuteItem = new(new NoOpCommand());
    private readonly ListItem secondItem = new(new NoOpCommand());
    private static Timer timer;

    public SampleUpdatingItemsPage()
    {
        Name = "Open";
        Icon = new IconInfo("\uE72C");
    }

    public override IListItem[] GetItems()
    {
        if (timer == null)
        {
            timer = new Timer(500);
            timer.Elapsed += (object source, ElapsedEventArgs e) =>
            {
                var current = DateAndTime.Now;
                hourItem.Title = $"{current.Hour}";
                minuteItem.Title = $"{current.Minute}";
                secondItem.Title = $"{current.Second}";
            };
            timer.AutoReset = true; // Keep repeating
            timer.Enabled = true;
        }

        return [hourItem, minuteItem, secondItem];
    }
}
