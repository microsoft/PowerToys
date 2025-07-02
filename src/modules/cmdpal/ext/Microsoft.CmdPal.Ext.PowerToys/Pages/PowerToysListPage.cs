// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Pages;

internal sealed partial class PowerToysListPage : DynamicListPage
{
    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        return;
    }

    public override IListItem[] GetItems() => [];
}
