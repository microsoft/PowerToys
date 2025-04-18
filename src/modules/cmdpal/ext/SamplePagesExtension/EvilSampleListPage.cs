// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class EvilSampleListPage : ListPage
{
    public EvilSampleListPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Open";
        Title = "Evil Sample Page";
    }

    public override IListItem[] GetItems()
    {
        IListItem[] commands = [
          new ListItem(new EvilSampleListPage())
           {
               Subtitle = "Doesn't matter, I'll blow up before you see this",
           },
        ];

        _ = commands[9001]; // Throws

        return commands;
    }
}
