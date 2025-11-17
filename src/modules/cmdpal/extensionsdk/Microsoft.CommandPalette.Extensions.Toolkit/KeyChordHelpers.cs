// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static partial class KeyChordHelpers
{
    public static KeyChord FromModifiers(
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        bool win = false,
        int vkey = 0,
        int scanCode = 0)
    {
        var modifiers = (ctrl ? VirtualKeyModifiers.Control : VirtualKeyModifiers.None)
            | (alt ? VirtualKeyModifiers.Menu : VirtualKeyModifiers.None)
            | (shift ? VirtualKeyModifiers.Shift : VirtualKeyModifiers.None)
            | (win ? VirtualKeyModifiers.Windows : VirtualKeyModifiers.None)
            ;
        return new(modifiers, vkey, scanCode);
    }

    public static KeyChord FromModifiers(
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        bool win = false,
        VirtualKey vkey = VirtualKey.None,
        int scanCode = 0)
    {
        return FromModifiers(ctrl, alt, shift, win, (int)vkey, scanCode);
    }

    public static string FormatForDebug(KeyChord value)
    {
        var result = string.Empty;

        if (value.Modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            result += "Ctrl+";
        }

        if (value.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            result += "Shift+";
        }

        if (value.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            result += "Alt+";
        }

        result += (VirtualKey)value.Vkey;

        return result;
    }
}
