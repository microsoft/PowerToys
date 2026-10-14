// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Maps focused dock-item keys to actions and creates dock-specific invocation messages.
/// </summary>
internal static class DockItemActions
{
    /// <summary>
    /// Handles extension shortcuts and dock action keys while preserving Enter and Space button activation.
    /// </summary>
    /// <param name="item">The focused dock item's cached command context.</param>
    /// <param name="chord">The key and modifiers to handle.</param>
    /// <param name="invoke">Invokes the selected primary, secondary, or extension command.</param>
    /// <param name="openMenu">Requests a submenu, or the root menu when passed null; the caller defers until key routing finishes.</param>
    /// <returns>True when the key is consumed, including Ctrl+Enter when no secondary action exists.</returns>
    public static bool TryHandleKey(
        CommandItemViewModel item,
        KeyChord chord,
        Action<CommandItemViewModel> invoke,
        Action<CommandContextItemViewModel?> openMenu)
    {
        if (((IContextMenuContext)item).FindKeybinding(chord) is { } command)
        {
            if (command.HasSubmenu)
            {
                openMenu(command);
            }
            else
            {
                invoke(command);
            }

            return true;
        }

        switch ((VirtualKey)chord.Vkey)
        {
            case VirtualKey.Enter when chord.Modifiers == VirtualKeyModifiers.Control:
                if (item.SecondaryCommand is { } secondary)
                {
                    invoke(secondary);
                }

                break;
            case VirtualKey.Enter:
            case VirtualKey.Space:
                // Preserve button activation for unclaimed modified Enter/Space chords.
                invoke(item);
                break;
            case VirtualKey.K when chord.Modifiers == VirtualKeyModifiers.Control && item.CanOpenContextMenu:
                openMenu(null);
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a transient invocation without animation, preserving sender context only for menu actions.
    /// </summary>
    /// <remarks>Primary dock activation omits the sender so it does not enter the home page's command history.</remarks>
    public static PerformCommandMessage CreateInvocationMessage(CommandItemViewModel item)
    {
        // Primary dock activation has no sender and must not become a home-page history entry.
        var message = item is CommandContextItemViewModel contextItem
            ? new PerformCommandMessage(contextItem)
            : new PerformCommandMessage(item.Command.Model);
        message.WithAnimation = false;
        message.TransientPage = true;
        return message;
    }

    /// <summary>
    /// Returns whether the cached command is set and needs the palette rather than direct invocation.
    /// </summary>
    public static bool ShouldShowPalette(CommandViewModel command) => command.IsSet && !command.IsInvokableCommand;
}
