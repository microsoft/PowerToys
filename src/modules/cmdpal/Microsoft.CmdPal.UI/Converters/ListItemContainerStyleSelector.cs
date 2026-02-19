// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class ListItemContainerStyleSelector : StyleSelector
{
    public ListItemViewMode ListItemViewMode { get; set; }

    public Style? TwoRowItem { get; set; }

    public Style? SingleRowItem { get; set; }

    public Style? Section { get; set; }

    public Style? Separator { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container)
    {
        var itemContainerStyle = ListItemViewMode == ListItemViewMode.SingleRow ? SingleRowItem : TwoRowItem;

        if (item is not ListItemViewModel element)
        {
            return itemContainerStyle;
        }

        switch (element.Type)
        {
            case ListItemType.Separator:
                return Separator;
            case ListItemType.SectionHeader:
                return Section;
            case ListItemType.Item:
            default:
                return itemContainerStyle;
        }
    }
}
