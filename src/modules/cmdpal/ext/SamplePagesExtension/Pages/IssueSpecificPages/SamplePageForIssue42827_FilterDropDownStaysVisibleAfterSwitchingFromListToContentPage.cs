// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages.IssueSpecificPages;

internal sealed partial class SamplePageForIssue42827_FilterDropDownStaysVisibleAfterSwitchingFromListToContentPage : DynamicListPage
{
    public SamplePageForIssue42827_FilterDropDownStaysVisibleAfterSwitchingFromListToContentPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Issue 42827 - Filters not hiding when navigating between pages";
        IsLoading = true;
        var filters = new SampleFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;
    }

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args) => RaiseItemsChanged();

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var items = SearchText.ToCharArray().Select(ch => new ListItem(new SampleContentPage()) { Title = ch.ToString() }).ToArray();
        if (items.Length == 0)
        {
            items = [
                new ListItem(new SampleContentPage()) { Title = "This List item will open a content page" },
                new ListItem(new SampleContentPage()) { Title = "This List item will open a content page too" },
                new ListItem(new SampleContentPage()) { Title = "Guess what this one will do?" },
            ];
        }

        if (!string.IsNullOrEmpty(Filters.CurrentFilterId))
        {
            switch (Filters.CurrentFilterId)
            {
                case "mod2":
                    items = items.Where((item, index) => (index + 1) % 2 == 0).ToArray();
                    break;
                case "mod3":
                    items = items.Where((item, index) => (index + 1) % 3 == 0).ToArray();
                    break;
                case "all":
                default:
                    // No filtering
                    break;
            }
        }

        foreach (var item in items)
        {
            item.Subtitle = "Filter drop-down should be hidden when navigating to a content page";
        }

        return items;
    }

    internal sealed partial class SampleFilters : Filters
    {
        public override IFilterItem[] GetFilters()
        {
            return
            [
                new Filter() { Id = "all", Name = "All" },
                new Filter() { Id = "mod2", Name = "Every 2nd", Icon = new IconInfo("2") },
                new Filter() { Id = "mod3", Name = "Every 3rd (and long name)", Icon = new IconInfo("3") },
            ];
        }
    }
}
