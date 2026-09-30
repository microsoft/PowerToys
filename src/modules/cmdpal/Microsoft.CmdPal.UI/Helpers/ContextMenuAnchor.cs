// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Specifies a menu's placement and filter position for its owning surface.
/// </summary>
/// <param name="Element">The loaded element used as the placement target.</param>
/// <param name="Position">An optional point in the target's coordinates; null uses element-relative placement.</param>
/// <param name="Placement">The preferred placement, subject to screen bounds.</param>
/// <param name="FilterLocation">The filter box's position within the menu.</param>
public readonly record struct ContextMenuAnchor(
    FrameworkElement Element,
    Point? Position,
    FlyoutPlacementMode Placement,
    ContextMenuFilterLocation FilterLocation);
