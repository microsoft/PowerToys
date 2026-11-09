// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Services;

internal sealed partial class ContextMenuHost
{
    /// <summary>
    /// Creates a host for a surface's reusable flyout.
    /// </summary>
    /// <param name="defaultAnchor">Resolves the current layout when a request has no explicit anchor.</param>
    /// <param name="flyout">The flyout used for every menu on this surface.</param>
    internal ContextMenuHost(
        Func<ContextMenuAnchor> defaultAnchor,
        ContextMenuFlyout flyout)
        : this(
            callback => flyout.DispatcherQueue.TryEnqueue(() => callback()),
            request =>
            {
                var anchor = request.Anchor ?? defaultAnchor();

                flyout.ShowAt(
                    anchor.Element,
                    request.Context,
                    anchor.FilterLocation,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = anchor.Placement,
                        Position = anchor.Position,
                    },
                    request.InitialSubmenu,
                    request.ShowFilterBox);
            },
            flyout.Close)
    {
    }
}
