// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Parses and formats friendly key and shortcut strings, converting between
/// the human-readable representation used in the DSC configuration (e.g.
/// "Ctrl+Shift+A" or "Win+O, K" for a chord) and the semicolon-separated
/// decimal virtual-key strings stored in the Keyboard Manager profile.
/// </summary>
public static class KbmShortcutParser
{
    private const char KeySeparator = '+';
    private const char ChordSeparator = ',';
    private const char VkSeparator = ';';

    /// <summary>
    /// Result of parsing a friendly key or shortcut string: the virtual-key
    /// codes in canonical order and, for chords, the second chord key.
    /// </summary>
    /// <param name="Keys">The virtual-key codes in canonical serialization order.</param>
    /// <param name="SecondKeyOfChord">The second chord key, or 0 when the shortcut has no chord.</param>
    public sealed record ParsedKeys(IReadOnlyList<uint> Keys, uint SecondKeyOfChord)
    {
        /// <summary>Gets a value indicating whether the parsed value is a single key.</summary>
        public bool IsSingleKey => Keys.Count == 1 && SecondKeyOfChord == 0;

        /// <summary>Gets the semicolon-separated decimal virtual-key string stored in the profile.</summary>
        public string ToVkString()
        {
            return string.Join(VkSeparator, Keys.Select(k => k.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// Tries to parse a friendly single-key name.
    /// </summary>
    /// <param name="input">The friendly key name.</param>
    /// <param name="result">The parse result.</param>
    /// <param name="error">The error message, if parsing failed.</param>
    /// <returns>True on success; otherwise false.</returns>
    public static bool TryParseKey(string? input, out ParsedKeys result, out string error)
    {
        result = new ParsedKeys([], 0);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Key name is empty";
            return false;
        }

        if (!KbmKeyNames.TryGetCode(input, out var code))
        {
            error = $"Invalid key name '{input.Trim()}'";
            return false;
        }

        result = new ParsedKeys([code], 0);
        return true;
    }

    /// <summary>
    /// Tries to parse a friendly key or shortcut string. A shortcut consists
    /// of parts joined by '+' with at most one action key, and may carry a
    /// chord second key after a comma (e.g. "Win+O, K"). Single keys are also
    /// accepted so remap targets can be either form.
    /// </summary>
    /// <param name="input">The friendly key or shortcut string.</param>
    /// <param name="result">The parse result, with keys ordered Win, Ctrl, Alt, Shift, action[, chord].</param>
    /// <param name="error">The error message, if parsing failed.</param>
    /// <returns>True on success; otherwise false.</returns>
    public static bool TryParseKeyOrShortcut(string? input, out ParsedKeys result, out string error)
    {
        result = new ParsedKeys([], 0);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Shortcut is empty";
            return false;
        }

        // Split off an optional chord second key: "Win+O, K"
        var chordParts = input.Split(ChordSeparator);
        if (chordParts.Length > 2)
        {
            error = $"Shortcut '{input.Trim()}' has more than one chord separator (',')";
            return false;
        }

        uint secondKeyOfChord = 0;
        if (chordParts.Length == 2)
        {
            var chordName = chordParts[1].Trim();
            if (chordName.Contains(KeySeparator, StringComparison.Ordinal))
            {
                error = $"Chord key '{chordName}' must be a single key";
                return false;
            }

            if (!KbmKeyNames.TryGetCode(chordName, out secondKeyOfChord))
            {
                error = $"Invalid chord key name '{chordName}'";
                return false;
            }

            if (KbmKeyNames.IsModifier(secondKeyOfChord))
            {
                error = $"Chord key '{chordName}' cannot be a modifier";
                return false;
            }
        }

        // Parse the main part: modifiers plus at most one action key
        var modifiers = new SortedDictionary<KbmKeyNames.ModifierClass, uint>();
        uint actionKey = 0;
        string? actionName = null;
        foreach (var part in chordParts[0].Split(KeySeparator))
        {
            var keyName = part.Trim();
            if (keyName.Length == 0)
            {
                error = $"Shortcut '{input.Trim()}' contains an empty key part";
                return false;
            }

            if (!KbmKeyNames.TryGetCode(keyName, out var code))
            {
                error = $"Invalid key name '{keyName}'";
                return false;
            }

            var modifierClass = KbmKeyNames.GetModifierClass(code);
            if (modifierClass == KbmKeyNames.ModifierClass.None)
            {
                if (actionKey != 0)
                {
                    error = $"Shortcut '{input.Trim()}' has more than one action key ('{actionName}' and '{keyName}')";
                    return false;
                }

                actionKey = code;
                actionName = keyName;
            }
            else
            {
                if (modifiers.ContainsKey(modifierClass))
                {
                    error = $"Shortcut '{input.Trim()}' repeats the {modifierClass} modifier";
                    return false;
                }

                modifiers.Add(modifierClass, code);
            }
        }

        if (actionKey == 0)
        {
            error = $"Shortcut '{input.Trim()}' has no action key";
            return false;
        }

        if (secondKeyOfChord != 0 && modifiers.Count == 0)
        {
            error = $"Chord shortcut '{input.Trim()}' requires at least one modifier";
            return false;
        }

        // Canonical serialization order: Win, Ctrl, Alt, Shift, action, chord
        // (matches Shortcut::ToHstringVK so DSC-written strings are identical
        // to editor-written ones). SortedDictionary keys iterate in enum order.
        var keys = new List<uint>(modifiers.Values) { actionKey };
        if (secondKeyOfChord != 0)
        {
            keys.Add(secondKeyOfChord);
        }

        result = new ParsedKeys(keys, secondKeyOfChord);
        return true;
    }

    /// <summary>
    /// Tries to parse a stored semicolon-separated decimal virtual-key string
    /// into key codes. When <paramref name="secondKeyOfChord"/> is non-zero,
    /// the trailing key is treated as the chord second key (the engine embeds
    /// it as the last element of the stored string).
    /// </summary>
    /// <param name="vkString">The stored virtual-key string, e.g. "162;65".</param>
    /// <param name="secondKeyOfChord">The chord second key from the profile entry, or 0.</param>
    /// <param name="result">The parse result.</param>
    /// <returns>True on success; otherwise false.</returns>
    public static bool TryParseVkString(string? vkString, uint secondKeyOfChord, out ParsedKeys result)
    {
        result = new ParsedKeys([], 0);
        if (string.IsNullOrWhiteSpace(vkString))
        {
            return false;
        }

        var keys = new List<uint>();
        foreach (var part in vkString.Split(VkSeparator))
        {
            if (!uint.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code == 0)
            {
                return false;
            }

            keys.Add(code);
        }

        // The chord second key is stored as the trailing element of the key
        // string (see Shortcut::SetKeyCodes); it is also mirrored in the
        // secondKeyOfChord property by the Settings UI models.
        var chordKey = 0u;
        if (secondKeyOfChord != 0 && keys.Count >= 2 && keys[^1] == secondKeyOfChord)
        {
            chordKey = secondKeyOfChord;
        }

        result = new ParsedKeys(keys, chordKey);
        return true;
    }

    /// <summary>
    /// Reorders parsed keys into the canonical serialization order (Win,
    /// Ctrl, Alt, Shift, action, chord). Stored profile entries written by
    /// the editor already use this order, but hand-edited profiles may not.
    /// </summary>
    /// <param name="keys">The parsed keys.</param>
    /// <returns>The keys in canonical order.</returns>
    public static ParsedKeys Canonicalize(ParsedKeys keys)
    {
        var ordered = keys.Keys
            .OrderBy(k => KbmKeyNames.GetModifierClass(k) == KbmKeyNames.ModifierClass.None
                ? (k == keys.SecondKeyOfChord ? int.MaxValue : int.MaxValue - 1)
                : (int)KbmKeyNames.GetModifierClass(k))
            .ToList();
        return new ParsedKeys(ordered, keys.SecondKeyOfChord);
    }

    /// <summary>
    /// Formats key codes as the canonical friendly string: parts joined by
    /// '+', with the chord second key appended after ", " when present.
    /// </summary>
    /// <param name="keys">The parsed keys.</param>
    /// <returns>The canonical friendly string.</returns>
    public static string Format(ParsedKeys keys)
    {
        if (keys.SecondKeyOfChord != 0)
        {
            var mainKeys = keys.Keys.Take(keys.Keys.Count - 1).Select(KbmKeyNames.GetName);
            return $"{string.Join(KeySeparator, mainKeys)}{ChordSeparator} {KbmKeyNames.GetName(keys.SecondKeyOfChord)}";
        }

        return string.Join(KeySeparator, keys.Keys.Select(KbmKeyNames.GetName));
    }
}
