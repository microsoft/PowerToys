// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

internal static class MacroHotkeyConverter
{
    private static readonly Dictionary<string, int> NameToVk;
    private static readonly Dictionary<int, string> VkToName;

    static MacroHotkeyConverter()
    {
        NameToVk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enter"] = 0x0D,
            ["Tab"] = 0x09,
            ["Space"] = 0x20,
            ["Backspace"] = 0x08,
            ["Escape"] = 0x1B,
            ["Esc"] = 0x1B,
            ["Delete"] = 0x2E,
            ["Del"] = 0x2E,
            ["Insert"] = 0x2D,
            ["Ins"] = 0x2D,
            ["Home"] = 0x24,
            ["End"] = 0x23,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["Left"] = 0x25,
            ["Right"] = 0x27,
            ["Up"] = 0x26,
            ["Down"] = 0x28,
            ["F1"] = 0x70,
            ["F2"] = 0x71,
            ["F3"] = 0x72,
            ["F4"] = 0x73,
            ["F5"] = 0x74,
            ["F6"] = 0x75,
            ["F7"] = 0x76,
            ["F8"] = 0x77,
            ["F9"] = 0x78,
            ["F10"] = 0x79,
            ["F11"] = 0x7A,
            ["F12"] = 0x7B,
        };

        for (int i = 0; i < 26; i++)
        {
            NameToVk[((char)('A' + i)).ToString()] = 0x41 + i;
            NameToVk[((char)('a' + i)).ToString()] = 0x41 + i;
        }

        for (int i = 0; i < 10; i++)
        {
            NameToVk[((char)('0' + i)).ToString()] = 0x30 + i;
        }

        VkToName = new Dictionary<int, string>();
        foreach (var (name, vk) in NameToVk)
        {
            VkToName.TryAdd(vk, name);
        }
    }

    public static HotkeySettings ToHotkeySettings(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return new HotkeySettings();
        }

        var parts = hotkey.Split('+');
        if (parts.Length == 0)
        {
            return new HotkeySettings();
        }

        var mods = parts[..^1];
        var key = parts[^1].Trim();

        bool win = mods.Any(m => m.Trim().Equals("Win", StringComparison.OrdinalIgnoreCase));
        bool ctrl = mods.Any(m => m.Trim().Equals("Ctrl", StringComparison.OrdinalIgnoreCase));
        bool alt = mods.Any(m => m.Trim().Equals("Alt", StringComparison.OrdinalIgnoreCase));
        bool shift = mods.Any(m => m.Trim().Equals("Shift", StringComparison.OrdinalIgnoreCase));
        int code = NameToVk.TryGetValue(key, out int vk) ? vk : 0;

        return new HotkeySettings(win, ctrl, alt, shift, code);
    }

    public static string? FromHotkeySettings(HotkeySettings? hs)
    {
        if (hs is null || hs.Code == 0)
        {
            return null;
        }

        if (!VkToName.TryGetValue(hs.Code, out string? keyName))
        {
            return null;
        }

        var parts = new List<string>();
        if (hs.Win)
        {
            parts.Add("Win");
        }

        if (hs.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (hs.Alt)
        {
            parts.Add("Alt");
        }

        if (hs.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(keyName);
        return string.Join("+", parts);
    }
}
