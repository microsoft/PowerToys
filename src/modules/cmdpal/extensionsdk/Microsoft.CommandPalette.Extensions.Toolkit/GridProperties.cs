// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class GridProperties : IGridProperties
{
    public GridProperties(double width = 100, double height = 100)
    {
        TileSize = new Size(width, height);
    }

    public Size TileSize { get; set; }
}
