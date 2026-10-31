// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Presents context menus for one shell surface, so requests never reach another shell's menu.
/// </summary>
/// <remarks>Call on the UI thread.</remarks>
internal interface IContextMenuOwner
{
    /// <summary>
    /// Opens the supplied request, superseding any deferred keyboard open.
    /// </summary>
    void ShowContextMenu(ContextMenuRequest request);

    /// <summary>
    /// Opens a menu for <paramref name="context"/> after the triggering key finishes routing,
    /// unless the request is superseded, closed, or the context is no longer current.
    /// </summary>
    /// <param name="context">The menu to open, such as the selected item's menu or the current page menu.</param>
    /// <param name="initialSubmenu">A root command whose submenu opens first; the open is cancelled if <paramref name="context"/> no longer contains it.</param>
    /// <param name="anchor">The placement override, or null to use the owner's default anchor.</param>
    /// <param name="isContextCurrent">Revalidates <paramref name="context"/> when the open is dispatched, or null to require the live selection.</param>
    void ShowContextMenuAfterKeyEvent(
        IContextMenuContext context,
        CommandContextItemViewModel? initialSubmenu = null,
        ContextMenuAnchor? anchor = null,
        Func<bool>? isContextCurrent = null);

    /// <summary>
    /// Closes the owner's menu and cancels its pending opens.
    /// </summary>
    void CloseContextMenu();
}
