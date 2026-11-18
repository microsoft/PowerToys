// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Graphics;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Captures the anchor and target display geometry for one flyout presentation.
/// Rectangles use absolute screen physical-pixel coordinates. A zero-size anchor represents a point.
/// </summary>
/// <param name="DisplayIdValue">The target display identifier.</param>
/// <param name="Anchor">The notification icon rectangle or a point on the target work area edge.</param>
/// <param name="OuterBounds">The target display bounds.</param>
/// <param name="WorkArea">The target display work area.</param>
/// <param name="DpiScale">The target display effective DPI divided by 96.</param>
/// <param name="TaskbarEdge">The taskbar edge, independent of the anchor's position.</param>
public sealed record FlyoutPlacementContext(
    ulong DisplayIdValue,
    RectInt32 Anchor,
    RectInt32 OuterBounds,
    RectInt32 WorkArea,
    double DpiScale,
    FlyoutPlacementEdge TaskbarEdge);
