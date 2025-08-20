// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class GridItemTemplateSelector : DataTemplateSelector
{
    public IGridProperties? GridProperties { get; set; }

    public DataTemplate? Small { get; set; }

    public DataTemplate? Medium { get; set; }

    public DataTemplate? Gallery { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Medium;

        if (GridProperties is ISmallGridLayout)
        {
            dataTemplate = Small;
        }
        else if (GridProperties is IMediumGridLayout)
        {
            dataTemplate = Medium;
        }
        else if (GridProperties is IGalleryGridLayout)
        {
            dataTemplate = Gallery;
        }

        return dataTemplate;
    }
}
