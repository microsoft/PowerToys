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

    public DataTemplate? ListItem { get; set; }

    public DataTemplate? Separator { get; set; }

    public DataTemplate? Section { get; set; }

    public DataTemplate? SingleRowListItem { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        DataTemplate? dataTemplate = ListItemViewMode == ListItemViewMode.Singleline ? SingleRowListItem : ListItem;

        if (container is not ListViewItem listItem)
        {
            return dataTemplate;
        }

        if (item is not ListItemViewModel element)
        {
            return dataTemplate;
        }

        if (container is ListViewItem li && element.IsSectionOrSeparator)
        {
            li.IsEnabled = false;
            li.AllowFocusWhenDisabled = false;
            li.AllowFocusOnInteraction = false;
            li.IsHitTestVisible = false;
            dataTemplate = string.IsNullOrWhiteSpace(element.Section) ? Separator : Section;
        }
        else
        {
            listItem.IsEnabled = true;
            listItem.AllowFocusWhenDisabled = true;
            listItem.AllowFocusOnInteraction = true;
            listItem.IsHitTestVisible = true;
        }

        return dataTemplate;
    }
}
