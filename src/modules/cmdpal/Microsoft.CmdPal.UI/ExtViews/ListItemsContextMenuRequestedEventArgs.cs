// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI;

public sealed class ListItemsContextMenuRequestedEventArgs(
    ICommandBarContext context,
    FrameworkElement element,
    FlyoutPlacementMode placement,
    Point position,
    ContextMenuFilterLocation filterLocation) : EventArgs
{
    public ICommandBarContext Context { get; } = context;

    public FrameworkElement Element { get; } = element;

    public FlyoutPlacementMode Placement { get; } = placement;

    public Point Position { get; } = position;

    public ContextMenuFilterLocation FilterLocation { get; } = filterLocation;
}
