// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Maps dock placement to the preferred context-menu placement.
/// </summary>
internal static class FlyoutPlacementHelper
{
    /// <summary>
    /// Prefers upward menus for a bottom dock, downward menus for other sides, and Auto for unknown sides.
    /// </summary>
    internal static FlyoutPlacementMode ForDockSide(DockSide side) => side switch
    {
        DockSide.Top or DockSide.Left or DockSide.Right => FlyoutPlacementMode.BottomEdgeAlignedLeft,
        DockSide.Bottom => FlyoutPlacementMode.TopEdgeAlignedLeft,
        _ => FlyoutPlacementMode.Auto,
    };
}
