// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerToys.Helper;
using Microsoft.CmdPal.Ext.PowerToys.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Pages;

internal sealed partial class PowerToysListPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public PowerToysListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.svg");
        Name = Title = Resources.PowerToysPage_Title;
        Id = "com.microsoft.cmdpal.powertoys";
        _emptyMessage = new CommandItem()
        {
            Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.svg"),
            Title = Resources.PowerToysPage_NotFoundModule,
            Subtitle = SearchText,
        };
        EmptyContent = _emptyMessage;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyMessage.Subtitle = newSearch;
        RaiseItemsChanged(0);
        return;
    }

    public override IListItem[] GetItems() => ModuleItemsHelper.FilteredItems(SearchText);
}
