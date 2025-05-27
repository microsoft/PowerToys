// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Timers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class ExplodeInFiveSeconds : ListPage
{
    private readonly bool _repeat;

    private IListItem[] Commands => [
      new ListItem(new NoOpCommand())
           {
               Title = "This page will explode in five seconds!",
               Subtitle = _repeat ? "Not only that, I'll _keep_ exploding every 5 seconds after that" : string.Empty,
           },
        ];

    private bool shouldExplode;
    private static Timer timer;

    public ExplodeInFiveSeconds(bool repeat)
    {
        _repeat = repeat;
        Icon = new IconInfo(string.Empty);
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        if (shouldExplode)
        {
            _ = Commands[9001]; // Throws
        }
        else
        {
            timer = new Timer(5000);
            timer.Elapsed += (object source, ElapsedEventArgs e) => { RaiseItemsChanged(9000); };
            timer.AutoReset = _repeat; // Keep repeating
            timer.Enabled = true;
        }

        shouldExplode = true;
        return Commands;
    }
}
