// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.Ext.WindowsServices.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsServices;

internal sealed partial class ServicesListPage : DynamicListPage
{
    public ServicesListPage()
    {
        Icon = WindowsServicesCommandsProvider.ServicesIcon;
        Name = "Windows Services";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0);

    public override IListItem[] GetItems()
    {
        var items = ServiceHelper.Search(SearchText).ToArray();

        return items;
    }
}
