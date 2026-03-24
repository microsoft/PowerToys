// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

public sealed partial class SampleGoToLandingPage : ListPage
{
    public const string PageId = "com.microsoft.SamplePages.GoToLandingPage";

    public SampleGoToLandingPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "GoToPage Sample: Landing Page";
        Id = PageId;
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "You have arrived at the landing page!",
                Subtitle = "This page was navigated to via CommandResult.GoToPage()",
            },
            new ListItem(new NoOpCommand())
            {
                Title = "This is a sample landing page",
                Subtitle = "Extensions can use GoToPage to direct users here after completing an action",
            },
        ];
    }
}
