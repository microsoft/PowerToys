// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>
/// Used to announce the context menu should open
/// </summary>
public record OpenContextMenuMessage(FrameworkElement? Element, FlyoutPlacementMode? FlyoutPlacementMode, Point? Point, ContextMenuFilterLocation ContextMenuFilterLocation)
{
}

public enum ContextMenuFilterLocation
{
    Top,
    Bottom,
}
