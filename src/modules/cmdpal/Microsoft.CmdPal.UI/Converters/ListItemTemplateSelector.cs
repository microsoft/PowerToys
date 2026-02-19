// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ListItem { get; set; }

    public DataTemplate? Separator { get; set; }

    public DataTemplate? Section { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is not ListItemViewModel element)
        {
            return ListItem;
        }

        switch (element.Type)
        {
            case ListItemType.Separator:
                return Separator;
            case ListItemType.SectionHeader:
                return Section;
            case ListItemType.Item:
            default:
                return ListItem;
        }
    }
}
