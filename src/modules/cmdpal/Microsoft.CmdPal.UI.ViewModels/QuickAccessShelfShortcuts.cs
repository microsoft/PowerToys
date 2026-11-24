// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class QuickAccessShelfShortcuts
{
    public const int NumberedShortcutCount = NumberedItemShortcuts.ShortcutCount;

    public enum SelectionShortcutTarget
    {
        None,
        Visible,
        Unavailable,
    }

    public static int GetTopRowShortcutIndex(VirtualKey key) => NumberedItemShortcuts.GetTopRowShortcutIndex(key);

    public static bool IsSelectionAccessKey(bool ctrl, bool shift, bool win) =>
        shift &&
        !ctrl &&
        !win;

    public static SelectionShortcutTarget ResolveSelectionShortcut(
        KeyChord chord,
        int visibleItemCount,
        bool isAccessKeyModeActive)
    {
        var index = GetTopRowShortcutIndex((VirtualKey)chord.Vkey);
        var modifiers = chord.Modifiers;
        var isDirectAccessKey =
            (modifiers == VirtualKeyModifiers.Menu ||
             modifiers == (VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift)) &&
            index >= 0;
        var isLatchedAccessKeySequence =
            isAccessKeyModeActive &&
            (modifiers == VirtualKeyModifiers.None || modifiers == VirtualKeyModifiers.Shift) &&
            index >= 0;
        if (!isDirectAccessKey && !isLatchedAccessKeySequence)
        {
            return SelectionShortcutTarget.None;
        }

        return index < visibleItemCount
            ? SelectionShortcutTarget.Visible
            : SelectionShortcutTarget.Unavailable;
    }
}
