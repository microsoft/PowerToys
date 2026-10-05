// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Coordinates immediate and deferred menu requests for one UI surface.
/// </summary>
/// <remarks>Call on the UI thread. A newer request or close cancels a pending deferred request.</remarks>
internal sealed partial class ContextMenuHost
{
    private readonly Func<Action, bool> _enqueue;
    private readonly Action<ContextMenuRequest> _openMenu;
    private readonly Action _closeMenu;
    private long _requestVersion;

    /// <summary>
    /// Creates a host using the surface's dispatch, open, and close operations.
    /// </summary>
    /// <param name="enqueue">Queues a callback for a later UI dispatcher turn; returns false when unavailable.</param>
    /// <param name="openMenu">Opens or queues a menu for the supplied request.</param>
    /// <param name="closeMenu">Closes the surface's menu and cancels any queued flyout open.</param>
    internal ContextMenuHost(
        Func<Action, bool> enqueue,
        Action<ContextMenuRequest> openMenu,
        Action closeMenu)
    {
        _enqueue = enqueue;
        _openMenu = openMenu;
        _closeMenu = closeMenu;
    }

    /// <summary>
    /// Forwards a menu request immediately, superseding any deferred keyboard open.
    /// </summary>
    public void Show(ContextMenuRequest request)
    {
        _requestVersion++;
        _openMenu(request);
    }

    /// <summary>
    /// Defers opening until the triggering key has finished routing.
    /// </summary>
    /// <param name="createRequest">Revalidates and creates the request when dispatched; null cancels the open.</param>
    public void ShowAfterKeyEvent(Func<ContextMenuRequest?> createRequest)
    {
        var requestVersion = ++_requestVersion;

        // Finish routing the triggering key before moving focus into the flyout.
        _enqueue(() =>
        {
            if (requestVersion == _requestVersion && createRequest() is { } request)
            {
                _openMenu(request);
            }
        });
    }

    /// <summary>
    /// Closes the surface's menu and cancels pending opens.
    /// </summary>
    public void Close()
    {
        _requestVersion++;
        _closeMenu();
    }
}
