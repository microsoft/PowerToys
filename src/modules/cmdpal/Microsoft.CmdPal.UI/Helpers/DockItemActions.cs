// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class DockItemActions
{
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

    public static bool ShouldShowPalette(CommandViewModel command) => command.IsSet && !command.IsInvokableCommand;
}
