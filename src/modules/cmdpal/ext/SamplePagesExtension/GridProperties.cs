// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace SamplePagesExtension;

// Implementation of IGridProperties for testing
internal class GridProperties : IGridProperties
{
    public Size TileSize { get; set; }
}
