// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

public sealed partial class SampleLandingPage : ListPage
{
    public SampleLandingPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "This is a sample landing page to land for GotoPage call.";
        Id = "com.microsoft.SamplePages.LandingPage";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "This is a basic item in the list",
                Subtitle = "I don't do anything though",
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This is a sample landing page to land for GotoPage call.",
                Subtitle = "I don't do anything though",
            },
        ];
    }
}
