// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages.IssueSpecificPages;

internal sealed partial class AllIssueSamplesIndexPage : ListPage
{
    public AllIssueSamplesIndexPage()
    {
        Icon = new IconInfo("🐛");
        Name = "All Issue Samples Index Page";
    }

    public override IListItem[] GetItems()
    {
        return new IListItem[]
        {
            new ListItem(new SamplePageForIssue42827_FilterDropDownStaysVisibleAfterSwitchingFromListToContentPage())
            {
                Title = "Issue 42827 - Filter Drop Down Stays Visible After Switching From List To Content Page",
                Subtitle = "Repro steps: Open this page, open the filter dropdown, select a filter, navigate to a content page, navigate back to this page. The filter dropdown should be closed but it remains open.",
            },
        };
    }
}
