// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.CmdPal.Ext.PowerToys.Helper;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Pages;

internal sealed partial class AwakePage : DynamicListPage
{
    public AwakePage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Awake.png");
        Name = Title = "Awake";
        Id = "com.microsoft.cmdpal.powertoys.awake";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
        return;
    }

    public override IListItem[] GetItems() => WorkspaceItemsHelper.FilteredItems(SearchText);
}
