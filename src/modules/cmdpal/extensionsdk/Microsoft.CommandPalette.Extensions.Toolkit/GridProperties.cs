// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class GridProperties : IGridProperties
{
    public GridProperties(GridTileSize gridTileSize)
    {
        Size = gridTileSize;
    }

    public GridTileSize Size { get; set; }

    public bool ShowTitle { get; set; } = true;

    public bool ShowSubtitle { get; set; }
}
