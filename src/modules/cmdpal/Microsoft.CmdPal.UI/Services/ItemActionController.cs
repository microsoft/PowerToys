// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.Services;

// CsWinRT generates the other partial declaration for IDisposable's IClosable projection.
/// <summary>
/// Routes palette item keys and menu messages independently of the command bar's presentation.
/// </summary>
/// <remarks>Call on the UI thread and dispose with the owning shell.</remarks>
internal sealed partial class ItemActionController :
    IRecipient<OpenContextMenuMessage>,
    IRecipient<ClosePaletteContextMenuMessage>,
    IDisposable
{
    private readonly Func<ICommandBarContext?> _currentContext;
    private readonly Func<bool> _canAct;
    private readonly ContextMenuHost _menuHost;
    private readonly IMessenger _messenger;
    private bool _isDisposed;

    /// <summary>
    /// Creates the controller and subscribes to palette menu messages.
    /// </summary>
    /// <param name="currentContext">Returns the live selection, independently of debounced bar updates.</param>
    /// <param name="canAct">Reports whether the shell currently allows item actions.</param>
    /// <param name="menuHost">The palette's menu host.</param>
    /// <param name="messenger">The messenger used for menu requests and command activation.</param>
    internal ItemActionController(
        Func<ICommandBarContext?> currentContext,
        Func<bool> canAct,
        ContextMenuHost menuHost,
        IMessenger messenger)
    {
        _currentContext = currentContext;
        _canAct = canAct;
        _menuHost = menuHost;
        _messenger = messenger;

        _messenger.Register<OpenContextMenuMessage>(this);
        _messenger.Register<ClosePaletteContextMenuMessage>(this);
    }

    private bool CanAct => !_isDisposed && _canAct();

    /// <summary>
    /// Handles extension shortcuts during preview key routing, before the search box consumes them.
    /// </summary>
    /// <returns>True when the key is consumed by command invocation or a deferred submenu request.</returns>
    public bool TryHandleShortcut(KeyChord chord)
    {
        if (!CanAct || _currentContext() is not { } context ||
            context.FindKeybinding(chord) is not { } command)
        {
            return false;
        }

        if (command.HasSubmenu)
        {
            OpenMenuAfterKeyEvent(context, command);
        }
        else
        {
            _messenger.Send(new PerformCommandMessage(command));
        }

        return true;
    }

    /// <summary>
    /// Handles Enter, Ctrl+Enter, and Ctrl+K during normal key routing.
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
            case VirtualKey.Enter when chord.Modifiers == VirtualKeyModifiers.Control:
                _messenger.Send<ActivateSecondaryCommandMessage>();
                break;
            case VirtualKey.Enter when chord.Modifiers == VirtualKeyModifiers.None:
                _messenger.Send<ActivateSelectedListItemMessage>();
                break;
            case VirtualKey.K when chord.Modifiers == VirtualKeyModifiers.Control:
                if (_currentContext() is { } context)
                {
                    OpenMenuAfterKeyEvent(context);
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
    public void Receive(OpenContextMenuMessage message)
    {
        if (CanAct)
        {
            _menuHost.Show(message.Request);
        }
    }

    /// <summary>
    /// Closes the palette menu and cancels its pending opens, including while item actions are disabled.
    /// </summary>
    public void Receive(ClosePaletteContextMenuMessage message) => _menuHost.Close();

    private void OpenMenuAfterKeyEvent(ICommandBarContext context, CommandContextItemViewModel? initialSubmenu = null)
    {
        _menuHost.ShowAfterKeyEvent(
            () =>
            {
                if (!CanAct || !ReferenceEquals(context, _currentContext()))
                {
                    return null;
                }

                return new ContextMenuRequest(context)
                {
                    InitialSubmenu = initialSubmenu,
                };
            });
    }

    /// <summary>
    /// Stops handling item actions, unregisters messages, and closes the palette menu. Safe to call repeatedly.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _messenger.UnregisterAll(this);
        _menuHost.Close();
    }
}
