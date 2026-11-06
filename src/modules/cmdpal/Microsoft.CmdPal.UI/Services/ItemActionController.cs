// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.Services;

// CsWinRT generates the other partial declaration for IDisposable's IClosable projection.
/// <summary>
/// Routes palette item keys and menu requests independently of the command bar's presentation.
/// </summary>
/// <remarks>Call on the UI thread and dispose with the owning shell.</remarks>
internal sealed partial class ItemActionController : IContextMenuOwner, IDisposable
{
    private readonly Func<ICommandBarContext?> _currentContext;
    private readonly Func<bool> _canAct;
    private readonly ContextMenuHost _menuHost;
    private bool _isDisposed;

    /// <summary>
    /// Creates the controller for one shell's palette menu.
    /// </summary>
    /// <param name="currentContext">Returns the live selection, independently of debounced bar updates.</param>
    /// <param name="canAct">Reports whether the shell currently allows item actions.</param>
    /// <param name="menuHost">The palette's menu host.</param>
    internal ItemActionController(
        Func<ICommandBarContext?> currentContext,
        Func<bool> canAct,
        ContextMenuHost menuHost)
    {
        _currentContext = currentContext;
        _canAct = canAct;
        _menuHost = menuHost;
    }

    private bool CanAct => !_isDisposed && _canAct();

    /// <summary>
    /// Handles Ctrl+K during normal key routing. Page interaction targets handle Enter and Ctrl+Enter.
    /// </summary>
    /// <returns>True when the key is consumed, even if no applicable action or menu exists.</returns>
    public bool TryHandleKey(KeyChord chord)
    {
        if (!CanAct)
        {
            return false;
        }

        switch ((VirtualKey)chord.Vkey)
        {
            case VirtualKey.K when chord.Modifiers == VirtualKeyModifiers.Control:
                if (_currentContext() is { } context)
                {
                    ShowContextMenuAfterKeyEvent(context);
                }

                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// Opens the supplied context when item actions are allowed, superseding a deferred keyboard open.
    /// </summary>
    public void ShowContextMenu(ContextMenuRequest request)
    {
        if (CanAct)
        {
            _menuHost.Show(request);
        }
    }

    /// <summary>
    /// Closes the palette menu and cancels its pending opens, including while item actions are disabled.
    /// </summary>
    public void CloseContextMenu() => _menuHost.Close();

    /// <summary>
    /// Defers opening until the triggering key finishes routing. The open is cancelled when superseded,
    /// closed, disabled, or when <paramref name="context"/> is no longer current.
    /// </summary>
    /// <param name="context">The menu to open, such as the selected item's menu or the current page menu.</param>
    /// <param name="initialSubmenu">A root command whose submenu opens first; the open is cancelled if <paramref name="context"/> no longer contains it.</param>
    /// <param name="anchor">The placement override, or null to use the palette's default anchor.</param>
    /// <param name="isContextCurrent">Revalidates <paramref name="context"/> when the open is dispatched, or null to require the live selection.</param>
    public void ShowContextMenuAfterKeyEvent(
        IContextMenuContext context,
        CommandContextItemViewModel? initialSubmenu = null,
        ContextMenuAnchor? anchor = null,
        Func<bool>? isContextCurrent = null)
    {
        if (!CanAct)
        {
            return;
        }

        _menuHost.ShowAfterKeyEvent(
            () =>
            {
                if (!CanAct ||
                    !(isContextCurrent?.Invoke() ?? ReferenceEquals(context, _currentContext())) ||
                    (initialSubmenu is not null && !context.AllCommands.Contains(initialSubmenu)))
                {
                    return null;
                }

                return new ContextMenuRequest(context)
                {
                    InitialSubmenu = initialSubmenu,
                    Anchor = anchor,
                };
            });
    }

    /// <summary>
    /// Stops handling item actions and closes the palette menu. Safe to call repeatedly.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _menuHost.Close();
    }
}
