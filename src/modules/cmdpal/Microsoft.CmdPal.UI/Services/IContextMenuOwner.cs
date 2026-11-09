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
    void ShowContextMenuAfterKeyEvent(ICommandBarContext context, CommandContextItemViewModel? initialSubmenu = null);

    /// <summary>
    /// Closes the owner's menu and cancels its pending opens.
    /// </summary>
    void CloseContextMenu();
}
