// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// A sample page that demonstrates the CommandResult.GoToPage() feature.
/// It shows three different NavigationMode options:
///   Push     - adds the landing page on top of the current stack
///   GoBack   - goes back one level, then navigates to the landing page
///   GoHome   - clears the entire stack, then navigates to the landing page
/// </summary>
public sealed partial class SampleGoToPage : ListPage
{
    public SampleGoToPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "GoToPage Sample";
    }

    public override IListItem[] GetItems()
    {
        var pushArgs = new GoToPageArgs
        {
            PageId = SampleGoToLandingPage.PageId,
            NavigationMode = NavigationMode.Push,
        };

        var goBackArgs = new GoToPageArgs
        {
            PageId = SampleGoToLandingPage.PageId,
            NavigationMode = NavigationMode.GoBack,
        };

        var goHomeArgs = new GoToPageArgs
        {
            PageId = SampleGoToLandingPage.PageId,
            NavigationMode = NavigationMode.GoHome,
        };

        return [
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(pushArgs),
                })
            {
                Title = "Push: Navigate to landing page (keep back stack)",
                Subtitle = "Adds the landing page on top of the current navigation stack",
                Icon = new IconInfo("\uEA37"),
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(goBackArgs),
                })
            {
                Title = "GoBack: Go back one level, then navigate to landing page",
                Subtitle = "Removes one page from the back stack before navigating",
                Icon = new IconInfo("\uE760"),
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.GoToPage(goHomeArgs),
                })
            {
                Title = "GoHome: Clear stack, then navigate to landing page",
                Subtitle = "Clears the entire navigation stack before navigating",
                Icon = new IconInfo("\uE80F"),
            },
        ];
    }
}
