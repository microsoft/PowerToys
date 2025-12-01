// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class GridItemTemplateSelector : DataTemplateSelector
{
    public IGridPropertiesViewModel? GridProperties { get; set; }

    public DataTemplate? Small { get; set; }

    public DataTemplate? Medium { get; set; }

    public DataTemplate? Gallery { get; set; }

    public DataTemplate? Section { get; set; }

    public DataTemplate? Separator { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Medium;

        if (GridProperties is SmallGridPropertiesViewModel)
        {
            dataTemplate = Small;
        }
        else if (GridProperties is MediumGridPropertiesViewModel)
        {
            dataTemplate = Medium;
        }
        else if (GridProperties is GalleryGridPropertiesViewModel)
        {
            dataTemplate = Gallery;
        }

        if (item is ListItemViewModel element && element.IsSectionOrSeparator)
        {
            dataTemplate = string.IsNullOrWhiteSpace(element.Section) ? Separator : Section;

            if (dependencyObject is UIElement li)
            {
                li.IsTabStop = false;
                li.IsHitTestVisible = false;
            }
        }

        return dataTemplate;
    }
}
