// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helper;

namespace PowerToysExtension.Pages;

internal sealed partial class PowerToysListPage : DynamicListPage
{
    private readonly CommandItem _empty;

    public PowerToysListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.png");
        Name = Title = "PowerToys";
        Id = "com.microsoft.cmdpal.powertoys";
        _empty = new CommandItem()
        {
            Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.png"),
            Title = "No matching module found",
            Subtitle = SearchText,
        };
        EmptyContent = _empty;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _empty.Subtitle = newSearch;
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => ModuleItemsHelper.FilteredItems(SearchText);
}
