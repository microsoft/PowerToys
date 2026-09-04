// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Static, keyboard-layout-invariant, bidirectional mapping between friendly
/// key names and the virtual-key codes stored in the Keyboard Manager profile.
/// Names are parsed case-insensitively; emission always uses the canonical
/// spelling so that a value round-trips identically on every machine.
/// </summary>
public static class KbmKeyNames
{
    /// <summary>Virtual-key code used by Keyboard Manager to disable a key. See VK_DISABLED in shared_constants.h.</summary>
    public const uint VkDisabled = 0x100;

    /// <summary>Virtual-key code used by Keyboard Manager for the side-agnostic Win key. See VK_WIN_BOTH in shared_constants.h.</summary>
    public const uint VkWinBoth = 0x104;

    /// <summary>Bit set on virtual-key codes that originate from the numpad. See numpadOriginBit in keyboard_layout.cpp.</summary>
    public const uint NumpadOriginBit = 0x80000000;

    // Canonical name for every supported virtual-key code. Insertion order is
    // irrelevant; each code appears exactly once.
    private static readonly Dictionary<uint, string> _namesByCode = BuildNames();

    // Name (canonical + aliases) to virtual-key code, case-insensitive.
    private static readonly Dictionary<string, uint> _codesByName = BuildCodes();

    /// <summary>
    /// Gets the canonical friendly name for a virtual-key code. Codes without
    /// a table entry render as "VK&lt;decimal&gt;" so that every storable code
    /// has a stable, parseable representation.
    /// </summary>
    /// <param name="code">The virtual-key code.</param>
    /// <returns>The canonical friendly name.</returns>
    public static string GetName(uint code)
    {
        return _namesByCode.TryGetValue(code, out var name) ? name : $"VK{code.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Tries to resolve a friendly key name to its virtual-key code. Accepts
    /// canonical names, aliases (case-insensitive), "VK&lt;decimal&gt;" and
    /// "0x&lt;hex&gt;" literals.
    /// </summary>
    /// <param name="name">The friendly key name.</param>
    /// <param name="code">The resolved virtual-key code.</param>
    /// <returns>True if the name was resolved; otherwise false.</returns>
    public static bool TryGetCode(string? name, out uint code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        name = name.Trim();
        if (_codesByName.TryGetValue(name, out code))
        {
            return true;
        }

        // "VK<decimal>" literal, e.g. VK44
        if (name.StartsWith("VK", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(name.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out code) &&
            code > 0)
        {
            return true;
        }

        // "0x<hex>" literal, e.g. 0x2C
        if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code) &&
            code > 0)
        {
            return true;
        }

        code = 0;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the code is one of the modifier keys
    /// (generic or sided Win/Ctrl/Alt/Shift).
    /// </summary>
    /// <param name="code">The virtual-key code.</param>
    /// <returns>True if the code is a modifier key; otherwise false.</returns>
    public static bool IsModifier(uint code)
    {
        return GetModifierClass(code) != ModifierClass.None;
    }

    /// <summary>
    /// Gets the modifier class of a virtual-key code, or
    /// <see cref="ModifierClass.None"/> for action keys.
    /// </summary>
    /// <param name="code">The virtual-key code.</param>
    /// <returns>The modifier class.</returns>
    public static ModifierClass GetModifierClass(uint code)
    {
        return code switch
        {
            VkWinBoth or 91 or 92 => ModifierClass.Win,
            17 or 162 or 163 => ModifierClass.Ctrl,
            18 or 164 or 165 => ModifierClass.Alt,
            16 or 160 or 161 => ModifierClass.Shift,
            _ => ModifierClass.None,
        };
    }

    /// <summary>
    /// Modifier key classes in the canonical serialization order used by the
    /// Keyboard Manager engine (Shortcut::ToHstringVK): Win, Ctrl, Alt, Shift.
    /// </summary>
    public enum ModifierClass
    {
        None = 0,
        Win = 1,
        Ctrl = 2,
        Alt = 3,
        Shift = 4,
    }

    private static Dictionary<uint, string> BuildNames()
    {
        var names = new Dictionary<uint, string>
        {
            // Modifiers, generic and sided
            [17] = "Ctrl",
            [18] = "Alt",
            [16] = "Shift",
            [VkWinBoth] = "Win",
            [162] = "LCtrl",
            [163] = "RCtrl",
            [164] = "LAlt",
            [165] = "RAlt",
            [160] = "LShift",
            [161] = "RShift",
            [91] = "LWin",
            [92] = "RWin",

            // Keyboard Manager specials
            [VkDisabled] = "Disable",

            // Navigation and editing
            [3] = "Break",
            [8] = "Backspace",
            [9] = "Tab",
            [12] = "Clear",
            [13] = "Enter",
            [19] = "Pause",
            [20] = "CapsLock",
            [27] = "Esc",
            [32] = "Space",
            [33] = "PgUp",
            [34] = "PgDn",
            [35] = "End",
            [36] = "Home",
            [37] = "Left",
            [38] = "Up",
            [39] = "Right",
            [40] = "Down",
            [44] = "PrintScreen",
            [45] = "Insert",
            [46] = "Delete",
            [93] = "Apps",
            [95] = "Sleep",
            [144] = "NumLock",
            [145] = "ScrollLock",

            // OEM keys; canonical names reflect the key's US-layout position
            [186] = "Semicolon",
            [187] = "Equals",
            [188] = "Comma",
            [189] = "Minus",
            [190] = "Period",
            [191] = "Slash",
            [192] = "Backquote",
            [219] = "LBracket",
            [220] = "Backslash",
            [221] = "RBracket",
            [222] = "Quote",
            [226] = "OEM102",

            // Numpad
            [106] = "NumPadMultiply",
            [107] = "NumPadAdd",
            [108] = "NumPadSeparator",
            [109] = "NumPadSubtract",
            [110] = "NumPadDecimal",
            [111] = "NumPadDivide",

            // Numpad-origin variants of navigation keys (see keyboard_layout.cpp)
            [37 | NumpadOriginBit] = "NumPadLeft",
            [39 | NumpadOriginBit] = "NumPadRight",
            [38 | NumpadOriginBit] = "NumPadUp",
            [40 | NumpadOriginBit] = "NumPadDown",
            [45 | NumpadOriginBit] = "NumPadInsert",
            [46 | NumpadOriginBit] = "NumPadDelete",
            [33 | NumpadOriginBit] = "NumPadPgUp",
            [34 | NumpadOriginBit] = "NumPadPgDn",
            [36 | NumpadOriginBit] = "NumPadHome",
            [35 | NumpadOriginBit] = "NumPadEnd",
            [13 | NumpadOriginBit] = "NumPadEnter",
            [111 | NumpadOriginBit] = "NumPadSlash",

            // Browser and media keys
            [166] = "BrowserBack",
            [167] = "BrowserForward",
            [168] = "BrowserRefresh",
            [169] = "BrowserStop",
            [170] = "BrowserSearch",
            [171] = "BrowserFavorites",
            [172] = "BrowserHome",
            [173] = "VolumeMute",
            [174] = "VolumeDown",
            [175] = "VolumeUp",
            [176] = "MediaNext",
            [177] = "MediaPrev",
            [178] = "MediaStop",
            [179] = "MediaPlayPause",
            [180] = "LaunchMail",
            [181] = "LaunchMediaSelect",
            [182] = "LaunchApp1",
            [183] = "LaunchApp2",
        };

        // Letters A-Z
        for (var code = 65u; code <= 90u; code++)
        {
            names[code] = ((char)code).ToString();
        }

        // Digits 0-9
        for (var code = 48u; code <= 57u; code++)
        {
            names[code] = ((char)code).ToString();
        }

        // Function keys F1-F24
        for (var code = 112u; code <= 135u; code++)
        {
            names[code] = $"F{(code - 111).ToString(CultureInfo.InvariantCulture)}";
        }

        // Numpad digits
        for (var code = 96u; code <= 105u; code++)
        {
            names[code] = $"NumPad{(code - 96).ToString(CultureInfo.InvariantCulture)}";
        }

        return names;
    }

    private static Dictionary<string, uint> BuildCodes()
    {
        var codes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name) in _namesByCode)
        {
            codes[name] = code;
        }

        // Parse-only aliases; emission always uses the canonical names above.
        codes["Escape"] = 27;
        codes["Return"] = 13;
        codes["Control"] = 17;
        codes["Windows"] = VkWinBoth;
        codes["PageUp"] = 33;
        codes["PageDown"] = 34;
        codes["PrtScn"] = 44;
        codes["Ins"] = 45;
        codes["Del"] = 46;
        codes["Spacebar"] = 32;
        codes["Disabled"] = VkDisabled;

        // Punctuation characters for the OEM keys (US layout)
        codes[";"] = 186;
        codes["="] = 187;
        codes[","] = 188;
        codes["-"] = 189;
        codes["."] = 190;
        codes["/"] = 191;
        codes["`"] = 192;
        codes["["] = 219;
        codes["\\"] = 220;
        codes["]"] = 221;
        codes["'"] = 222;

        return codes;
    }
}
