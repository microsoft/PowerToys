// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class GridItemContainerStyleSelector : StyleSelector
{
    public IGridPropertiesViewModel? GridProperties { get; set; }

    public Style? Small { get; set; }

    public Style? Medium { get; set; }

    public Style? Gallery { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container)
    {
        return GridProperties switch
        {
            SmallGridPropertiesViewModel => Small,
            MediumGridPropertiesViewModel => Medium,
            GalleryGridPropertiesViewModel => Gallery,
            _ => Medium,
        };
    }
}
