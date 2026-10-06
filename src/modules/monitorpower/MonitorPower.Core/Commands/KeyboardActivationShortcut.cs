// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace MonitorPower;

internal sealed record KeyboardActivationShortcut(
    string Text,
    int VirtualKey,
    bool Win,
    bool Control,
    bool Alt,
    bool Shift)
{
    private static readonly IReadOnlyDictionary<string, int> NamedKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
    };

    public static bool TryParse(string? value, out KeyboardActivationShortcut? shortcut, out string error)
    {
        shortcut = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Enter a keyboard shortcut.";
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "The shortcut must include at least one modifier and one key.";
            return false;
        }

        var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in parts[..^1])
        {
            var normalized = modifier.ToLowerInvariant() switch
            {
                "win" or "windows" => "win",
                "ctrl" or "control" => "ctrl",
                "alt" => "alt",
                "shift" => "shift",
                _ => string.Empty,
            };

            if (normalized.Length == 0 || !modifiers.Add(normalized))
            {
                error = $"Unsupported or duplicate modifier '{modifier}'.";
                return false;
            }
        }

        if (!TryGetVirtualKey(parts[^1], out var virtualKey, out var keyName))
        {
            error = $"Unsupported shortcut key '{parts[^1]}'.";
            return false;
        }

        bool win = modifiers.Contains("win");
        bool control = modifiers.Contains("ctrl");
        bool alt = modifiers.Contains("alt");
        bool shift = modifiers.Contains("shift");
        if (IsReservedCombination(win, control, alt, shift, keyName))
        {
            error = "This shortcut is reserved by Windows.";
            return false;
        }

        var displayModifiers = new List<string>();
        if (win)
        {
            displayModifiers.Add("Win");
        }

        if (control)
        {
            displayModifiers.Add("Ctrl");
        }

        if (alt)
        {
            displayModifiers.Add("Alt");
        }

        if (shift)
        {
            displayModifiers.Add("Shift");
        }

        displayModifiers.Add(keyName);

        shortcut = new KeyboardActivationShortcut(
            string.Join(" + ", displayModifiers),
            virtualKey,
            win,
            control,
            alt,
            shift);
        return true;
    }

    private static bool TryGetVirtualKey(string key, out int virtualKey, out string displayName)
    {
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            displayName = key.ToUpperInvariant();
            virtualKey = char.ToUpperInvariant(key[0]);
            return true;
        }

        if (key.Length >= 2 && key[0] is 'F' or 'f' &&
            int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            virtualKey = 0x70 + functionNumber - 1;
            displayName = $"F{functionNumber}";
            return true;
        }

        if (NamedKeys.TryGetValue(key, out virtualKey))
        {
            displayName = char.ToUpperInvariant(key[0]) + key[1..];
            return true;
        }

        virtualKey = 0;
        displayName = string.Empty;
        return false;
    }

    private static bool IsReservedCombination(bool win, bool control, bool alt, bool shift, string key)
    {
        bool reservedWinKey = key is ("A" or "C" or "D" or "E" or "I" or "L" or "N" or "P" or "R" or "S" or "Tab" or "V" or "W" or "X" or "Z" or "Space");
        bool reservedAltKey = key is ("F4" or "Tab" or "Escape");

        return (win && !control && !alt && !shift && reservedWinKey) ||
               (win && !alt && control && shift && key == "B") ||
               (win && !control && !alt && shift && key == "S") ||
               (!win && !control && alt && reservedAltKey) ||
               (!win && control && !alt && key == "Escape") ||
               (!win && control && alt && key == "Delete");
    }
}
