// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Ext.PowerToys.Helper;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Pages;

internal sealed partial class ColorPickerListPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public ColorPickerListPage()
    {
        Icon = new IconInfo("\uE790");
        Name = Title = "Saved colors";
        Id = "com.microsoft.cmdpal.powertoys.colorpicker";
        _emptyMessage = new CommandItem()
        {
            Icon = new IconInfo("\uE790"),
            Title = "No colors found",
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

    public override IListItem[] GetItems() => ColorPickerHelper.GetColorItems(SearchText);
}
