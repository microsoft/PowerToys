// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleDynamicListPage : DynamicListPage
{
    public SampleDynamicListPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Dynamic List";
        IsLoading = true;
        var filters = new SampleFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;
    }

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args) => RaiseItemsChanged();

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var items = SearchText.ToCharArray().Select(ch => new ListItem(new NoOpCommand()) { Title = ch.ToString() }).ToArray();
        if (items.Length == 0)
        {
            items = [new ListItem(new NoOpCommand()) { Title = "Start typing in the search box" }];
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

        if (items.Length > 0)
        {
            items[0].Subtitle = "Notice how the number of items changes for this page when you type in the filter box";
        }

        return items;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
public partial class SampleFilters : Filters
#pragma warning restore SA1402 // File may only contain a single type
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
