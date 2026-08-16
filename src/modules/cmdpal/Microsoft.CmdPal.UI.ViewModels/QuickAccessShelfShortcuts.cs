// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    public static bool IsSelectionShortcut(VirtualKey key, bool ctrl, bool alt, bool shift, bool win) =>
        alt &&
        IsSelectionAccessKey(ctrl, shift, win) &&
        GetTopRowShortcutIndex(key) >= 0;

    public static SelectionShortcutTarget ResolveSelectionShortcut(
        VirtualKey key,
        bool ctrl,
        bool alt,
        bool shift,
        bool win,
        int visibleItemCount)
    {
        if (!IsSelectionShortcut(key, ctrl, alt, shift, win))
        {
            return SelectionShortcutTarget.None;
        }

        return GetTopRowShortcutIndex(key) < visibleItemCount
            ? SelectionShortcutTarget.Visible
            : SelectionShortcutTarget.Unavailable;
    }
}
