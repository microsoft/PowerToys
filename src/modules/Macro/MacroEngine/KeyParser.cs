// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroEngine;

internal static class KeyParser
{
    // MOD_* constants for RegisterHotKey
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;
    internal const uint ModNoRepeat = 0x4000;

    // VK_L* codes used by SendInput (left-hand variants)
    private const ushort VkLControl = 0xA2;
    private const ushort VkLMenu = 0xA4; // Alt
    private const ushort VkLShift = 0xA0;
    private const ushort VkLWin = 0x5B;

    private static readonly Dictionary<string, ushort> NameToVk =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Tab"] = 0x09,
        ["Escape"] = 0x1B,
        ["Esc"] = 0x1B,
        ["Space"] = 0x20,
        ["Backspace"] = 0x08,
        ["Delete"] = 0x2E,
        ["Del"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
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

    /// <summary>Parses "Ctrl+Shift+V" → (Modifiers, Vk) for RegisterHotKey.</summary>
    internal static (uint Modifiers, ushort Vk) ParseHotkey(string hotkey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkey, nameof(hotkey));

        uint modifiers = 0;
        ushort vk = 0;

        foreach (var part in hotkey.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifiers |= ModControl; break;
                case "ALT": modifiers |= ModAlt; break;
                case "SHIFT": modifiers |= ModShift; break;
                case "WIN": modifiers |= ModWin; break;
                default: vk = ParseKey(part); break;
            }
        }

        if (vk == 0)
        {
            throw new ArgumentException($"No main key found in hotkey: '{hotkey}'");
        }

        return (Modifiers: modifiers | ModNoRepeat, Vk: vk);
    }

    /// <summary>Parses a single key name or character → VK code for SendInput.</summary>
    internal static ushort ParseKey(string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName, nameof(keyName));

        if (NameToVk.TryGetValue(keyName, out var vk))
        {
            return vk;
        }

        if (keyName.Length == 1)
        {
            // A–Z and 0–9 have VK codes matching their ASCII/Unicode values.
            var c = char.ToUpperInvariant(keyName[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                return (ushort)c;
            }
        }

        throw new ArgumentException($"Unknown key name: '{keyName}'");
    }

    /// <summary>Parses "Ctrl+C" → list of modifier VK codes + main VK for SendInput key-combo.</summary>
    internal static (IReadOnlyList<ushort> ModifierKeys, ushort MainVk) ParseKeyCombo(string combo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(combo, nameof(combo));

        var modifierKeys = new List<ushort>();
        ushort mainVk = 0;

        foreach (var part in combo.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifierKeys.Add(VkLControl); break;
                case "ALT": modifierKeys.Add(VkLMenu); break;
                case "SHIFT": modifierKeys.Add(VkLShift); break;
                case "WIN": modifierKeys.Add(VkLWin); break;
                default: mainVk = ParseKey(part); break;
            }
        }

        if (mainVk == 0)
        {
            throw new ArgumentException($"No main key found in combo: '{combo}'");
        }

        return (ModifierKeys: modifierKeys, MainVk: mainVk);
    }
}
