// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class QuickAccessShelfItemActions
{
    public static bool ShouldHandleKey(KeyChord chord)
    {
        var key = (VirtualKey)chord.Vkey;
        if (key is VirtualKey.Tab or VirtualKey.Escape or VirtualKey.Application)
        {
            return false;
        }

        if (chord.Modifiers == VirtualKeyModifiers.None && key is (VirtualKey.Enter or VirtualKey.Space or
            VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down or
            VirtualKey.Home or VirtualKey.End))
        {
            return false;
        }

        if (key == VirtualKey.F10 && chord.Modifiers == VirtualKeyModifiers.Shift)
        {
            return false;
        }

        // Keep shelf access keys available to the native buttons.
        return chord.Modifiers != VirtualKeyModifiers.Menu || key is < VirtualKey.Number0 or > VirtualKey.Number9;
    }

    public static bool TryHandleKey(
        ListItemViewModel item,
        KeyChord chord,
        Action<PerformCommandMessage> invoke,
        Action<CommandContextItemViewModel?> openMenu)
    {
        if (!ShouldHandleKey(chord))
        {
            return false;
        }

        if (((IContextMenuContext)item).FindKeybinding(chord) is { } command)
        {
            if (command.HasSubmenu)
            {
                openMenu(command);
            }
            else
            {
                invoke(new PerformCommandMessage(command));
            }

            return true;
        }

        switch ((VirtualKey)chord.Vkey)
        {
            case VirtualKey.Enter when chord.Modifiers == VirtualKeyModifiers.Control:
                if (item.SecondaryCommand is { } secondary)
                {
                    invoke(new PerformCommandMessage(secondary.Command.Model, item.Model));
                }

                return true;
            case VirtualKey.K when chord.Modifiers == VirtualKeyModifiers.Control:
                if (item.CanOpenContextMenu)
                {
                    openMenu(null);
                }

                return true;
            default:
                return false;
        }
    }
}
