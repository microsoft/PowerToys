// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class GridItemContainerStyleSelector : StyleSelector
{
    public IGridPropertiesViewModel? GridProperties { get; set; }

    public Style? Small { get; set; }

    public Style? Medium { get; set; }

    public Style? Gallery { get; set; }

    public Style? Section { get; set; }

    public Style? Separator { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container)
    {
        if (item is not ListItemViewModel element)
        {
            return Medium;
        }

        switch (element.Type)
        {
            case ListItemType.Separator:
                return Separator;
            case ListItemType.SectionHeader:
                return Section;
            default:
                break;
        }

        return GridProperties switch
        {
            SmallGridPropertiesViewModel => Small,
            MediumGridPropertiesViewModel => Medium,
            GalleryGridPropertiesViewModel => Gallery,
            _ => Medium,
        };
    }
}
