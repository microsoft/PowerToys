// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListItemTemplateSelector : DataTemplateSelector
{
    public ListItemViewMode ListItemViewMode { get; set; }

    public DataTemplate? TwoRowItem { get; set; }

    public DataTemplate? SingleRowItem { get; set; }

    public DataTemplate? Section { get; set; }

    public DataTemplate? Separator { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        var dataTemplate = ListItemViewMode == ListItemViewMode.SingleRow ? SingleRowItem : TwoRowItem;

        if (item is not ListItemViewModel element)
        {
            return dataTemplate;
        }

        switch (element.Type)
        {
            case ListItemType.Separator:
                return Separator;
            case ListItemType.SectionHeader:
                return Section;
            case ListItemType.Item:
            default:
                return dataTemplate;
        }
    }
}
