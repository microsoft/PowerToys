// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

public sealed partial class SampleGoToPage : ListPage
{
    private int depth;

    public SampleGoToPage(int depth)
    {
        this.depth = depth;
        Icon = new IconInfo("\uEA37");
        Name = "Here is the " + depth + "th layer of the stack";
    }

    public override IListItem[] GetItems()
    {
        var goBackArgs = new GoToPageArgs
        {
            PageId = "com.microsoft.SamplePages.LandingPage",
            NavigationMode = NavigationMode.GoBack,
        };

        var goHomeArgs = new GoToPageArgs
        {
            PageId = "com.microsoft.SamplePages.LandingPage",
            NavigationMode = NavigationMode.GoHome,
        };

        var pushLandingPageArgs = new GoToPageArgs
        {
            PageId = "com.microsoft.SamplePages.LandingPage",
            NavigationMode = NavigationMode.Push,
        };

        return [
            new ListItem(new SampleGoToPage(depth + 1)),
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(goBackArgs),
                })
            {
                Title = "Go back and then go to landing page.",
                Icon = new IconInfo("\uEA37"),
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(goHomeArgs),
                })
            {
                Title = "Go back to home page and then go to landing page.",
                Icon = new IconInfo("\uEA37"),
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(pushLandingPageArgs),
                })
            {
                Title = "Push landing page.",
                Icon = new IconInfo("\uEA37"),
            },
        ];
    }
}
