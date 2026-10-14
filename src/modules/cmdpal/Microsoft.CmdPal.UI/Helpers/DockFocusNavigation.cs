// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class DockFocusNavigation
{
    internal static bool IsReverseShortcut(HotkeySettings? shortcut, KeyChord chord)
    {
        if (shortcut is null || shortcut.Code <= 0 || shortcut.Shift)
        {
            return false;
        }

        var reverseChord = KeyChordHelpers.FromModifiers(shortcut.Ctrl, shortcut.Alt, shift: true, shortcut.Win, shortcut.Code);
        return chord.Vkey == reverseChord.Vkey && chord.Modifiers == reverseChord.Modifiers;
    }

    internal static bool TryFocusAcrossDocks(IReadOnlyList<string> dockOrder, string startingDockId, bool moveFromCurrent, bool restoreLastFocus, Func<string, bool, bool, bool> tryFocus, Action<string>? resetFocus = null)
    {
        foreach (var dockId in dockOrder)
        {
            var isStartingDock = string.Equals(dockId, startingDockId, StringComparison.OrdinalIgnoreCase);
            if (tryFocus(dockId, isStartingDock && moveFromCurrent, isStartingDock && restoreLastFocus))
            {
                return true;
            }

            resetFocus?.Invoke(dockId);
        }

        return false;
    }

    internal static bool TryFocusNext(int itemCount, int focusedIndex, Func<int, bool> tryFocus, bool wrap = true, bool reverse = false, int rememberedIndex = -1)
    {
        var hasFocusedItem = focusedIndex >= 0 && focusedIndex < itemCount;
        var restoreIndex = !hasFocusedItem && rememberedIndex >= 0 && rememberedIndex < itemCount ? rememberedIndex : -1;
        if (restoreIndex >= 0 && tryFocus(restoreIndex))
        {
            return true;
        }

        var step = reverse ? -1 : 1;
        var startIndex = hasFocusedItem
            ? focusedIndex + step
            : reverse ? itemCount - 1 : 0;
        var attemptCount = wrap ? itemCount : reverse ? startIndex + 1 : itemCount - startIndex;

        for (var offset = 0; offset < attemptCount; offset++)
        {
            var index = (startIndex + (offset * step) + itemCount) % itemCount;
            if (index != restoreIndex && tryFocus(index))
            {
                return true;
            }
        }

        return false;
    }
}
