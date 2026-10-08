// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class NumberedItemShortcuts
{
    public const int ShortcutCount = 9;

    public enum ShortcutAction
    {
        Invoke,
        Select,
    }

    public readonly record struct Shortcut(int Index, ShortcutAction Action);

    public static Shortcut? Resolve(
        KeyChord chord,
        ShortcutAction plainAltAction,
        bool isAccessKeyModeActive)
    {
        var index = GetTopRowShortcutIndex((VirtualKey)chord.Vkey);
        var modifiers = chord.Modifiers;
        var isDirectAltChord =
            modifiers == VirtualKeyModifiers.Menu ||
            modifiers == (VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift);
        var isAccessKeySequence = isAccessKeyModeActive && modifiers is VirtualKeyModifiers.None or VirtualKeyModifiers.Shift;
        if (index < 0 || (!isDirectAltChord && !isAccessKeySequence))
        {
            return null;
        }

        return new(index, modifiers.HasFlag(VirtualKeyModifiers.Shift) ? ShortcutAction.Select : plainAltAction);
    }

    public static int GetTopRowShortcutIndex(VirtualKey key)
    {
        var index = (int)key - (int)VirtualKey.Number1;
        return index is >= 0 and < ShortcutCount ? index : -1;
    }

    public static IReadOnlyList<T> GetTargets<T>(IEnumerable<T> items, Func<T, bool> isEligible)
    {
        var targets = new List<T>(ShortcutCount);
        foreach (var item in items)
        {
            if (!isEligible(item))
            {
                continue;
            }

            targets.Add(item);
            if (targets.Count == ShortcutCount)
            {
                break;
            }
        }

        return targets;
    }

    public static string IndexToShortcutDigit(int index) =>
        index is >= 0 and < ShortcutCount
            ? (index + 1).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
}
