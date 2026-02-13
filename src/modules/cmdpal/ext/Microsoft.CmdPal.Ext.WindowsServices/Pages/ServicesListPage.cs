// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.WindowsServices.Helpers;
using Microsoft.CmdPal.Ext.WindowsServices.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsServices;

internal sealed partial class ServicesListPage : DynamicListPage
{
    public ServicesListPage()
    {
        Icon = Icons.ServicesIcon;
        Name = Resources.ServicesListPage_Name;

        var filters = new ServiceFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;
    }

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args) => RaiseItemsChanged();

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0);

    public override IListItem[] GetItems()
    {
        var items = ServiceHelper.Search(SearchText, Filters.CurrentFilterId).ToArray();

        return items;
    }
}
