// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>The key events that paste or type a snippet, as Text Expander sends them.</summary>
internal static class KeyStrokes
{
    internal const ushort Tab = 0x09;
    internal const ushort Return = 0x0D;
    internal const ushort Control = 0x11;
    internal const ushort V = 0x56;
    internal const ushort LeftWindows = 0x5B;
    internal const ushort RightWindows = 0x5C;
    internal const ushort LeftShift = 0xA0;
    internal const ushort RightShift = 0xA1;
    internal const ushort LeftControl = 0xA2;
    internal const ushort RightControl = 0xA3;
    internal const ushort LeftAlt = 0xA4;
    internal const ushort RightAlt = 0xA5;

    /// <summary>Gets the modifier keys checked before a paste or a typed Enter or Tab.</summary>
    internal static IReadOnlyList<ushort> Modifiers { get; } =
        [LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, LeftWindows, RightWindows];

    /// <summary>
    /// Gets Ctrl+V in one batch, after letting go of the modifiers the user holds, so it isn't sent as Ctrl+Shift+V.
    /// </summary>
    public static IReadOnlyList<KeyStroke> Paste(IReadOnlyList<ushort> heldModifiers)
    {
        ArgumentNullException.ThrowIfNull(heldModifiers);

        var strokes = new List<KeyStroke>(heldModifiers.Count + 4);
        foreach (var modifier in heldModifiers)
        {
            strokes.Add(KeyStroke.KeyUp(modifier));
        }

        strokes.Add(KeyStroke.KeyDown(Control));
        strokes.Add(KeyStroke.KeyDown(V));
        strokes.Add(KeyStroke.KeyUp(V));
        strokes.Add(KeyStroke.KeyUp(Control));
        return strokes;
    }

    /// <summary>
    /// Gets the key events that type <paramref name="text"/>, one batch per character as Text Expander types:
    /// a line break is Enter and a tab is Tab, since many apps ignore those as Unicode input, a surrogate pair goes as
    /// one batch, and every other character is Unicode input.
    /// </summary>
    public static IEnumerable<TypedCharacter> Typing(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                continue;
            }

            if (ch is '\n' or '\t')
            {
                var key = ch == '\n' ? Return : Tab;
                yield return new TypedCharacter([KeyStroke.KeyDown(key), KeyStroke.KeyUp(key)], IsKey: true, ch);
            }
            else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                var low = text[++i];
                yield return new TypedCharacter(
                    [KeyStroke.CharacterDown(ch), KeyStroke.CharacterUp(ch), KeyStroke.CharacterDown(low), KeyStroke.CharacterUp(low)],
                    IsKey: false,
                    ch);
            }
            else
            {
                yield return new TypedCharacter([KeyStroke.CharacterDown(ch), KeyStroke.CharacterUp(ch)], IsKey: false, ch);
            }
        }
    }

    /// <summary>
    /// Gets <paramref name="strokes"/> after letting go of <paramref name="heldModifiers"/>, so a typed Tab isn't Shift+Tab.
    /// </summary>
    public static IReadOnlyList<KeyStroke> AfterReleasing(IReadOnlyList<ushort> heldModifiers, IReadOnlyList<KeyStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(heldModifiers);
        ArgumentNullException.ThrowIfNull(strokes);

        if (heldModifiers.Count == 0)
        {
            return strokes;
        }

        var released = new List<KeyStroke>(heldModifiers.Count + strokes.Count);
        foreach (var modifier in heldModifiers)
        {
            released.Add(KeyStroke.KeyUp(modifier));
        }

        released.AddRange(strokes);
        return released;
    }
}
