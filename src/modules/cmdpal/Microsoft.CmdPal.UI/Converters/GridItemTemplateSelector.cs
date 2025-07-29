// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class GridItemTemplateSelector : DataTemplateSelector
{
    public GridTileSize? TileSize { get; set; }

    public DataTemplate? Small { get; set; }

    public DataTemplate? Medium { get; set; }

    public DataTemplate? Large { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Medium;

        switch (TileSize)
        {
            case GridTileSize.Small:
                dataTemplate = Small;
                break;
            case GridTileSize.Large:
                dataTemplate = Large;
                break;
            case GridTileSize.Medium:
            default:
                dataTemplate = Medium;
                break;
        }

        return dataTemplate;
    }
}
