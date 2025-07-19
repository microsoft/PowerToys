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
        Filters = new SampleDynamicMultiSelectFilters();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(newSearch.Length);

    public override IListItem[] GetItems()
    {
        var items = SearchText.ToCharArray().Select(ch => new ListItem(new NoOpCommand()) { Title = ch.ToString() }).ToArray();
        if (items.Length == 0)
        {
            items = [new ListItem(new NoOpCommand()) { Title = "Start typing in the search box" }];
        }

        if (items.Length > 0)
        {
            items[0].Subtitle = "Notice how the number of items changes for this page when you type in the filter box";
        }

        if (Filters?.CurrentFilterIds?.Length == 0)
        {
            return items;
        }

        ListItem[] filteredItems = [];
        if (Filters.CurrentFilterIds.Contains("mod2"))
        {
            filteredItems = items.Where((s, i) => i % 2 == 0).ToArray();
        }

        if (Filters.CurrentFilterIds.Contains("mod3"))
        {
            filteredItems = items.Where((s, i) => i % 3 == 0).Except(filteredItems).ToArray();
        }

        return filteredItems;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal sealed partial class SampleDynamicMultiSelectFilters
    : MultiSelectFilters
#pragma warning restore SA1402 // File may only contain a single type
{
    public SampleDynamicMultiSelectFilters()
    {
        CurrentFilterIds = [];
    }

    public override IFilterItem[] GetFilters()
    {
        return [
            new Filter() { Id = "mod2", Name = "Every 2nd char", Icon = new IconInfo("2") },
            new Filter() { Id = "mod3", Name = "Every 3rd char", Icon = new IconInfo("3") },
        ];
    }
}
