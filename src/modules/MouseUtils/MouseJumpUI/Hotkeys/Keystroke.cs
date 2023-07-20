// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MouseJumpUI.HotKeys;

public sealed class Keystroke
{
    public Keystroke(Keys key, KeyModifiers modifiers)
    {
        this.Key = key;
        this.Modifiers = modifiers;
    }

    public Keys Key
    {
        get;
    }

    public KeyModifiers Modifiers
    {
        get;
    }

    public static Keystroke Parse(string s)
    {
        if (!Keystroke.TryParse(s, out var result))
        {
            throw new ArgumentException("Invalid argument format.", nameof(s));
        }

        return result;
    }

    public static bool TryParse(string s, [NotNullWhen(true)] out Keystroke? result)
    {
        // see https://github.com/microsoft/terminal/blob/14919073a12fc0ecb4a9805cc183fdd68d30c4b6/src/cascadia/TerminalSettingsModel/KeyChordSerialization.cpp#L124
        // for an alternate implementation

        // e.g. "CTRL + ALT + SHIFT + F"
        if (string.IsNullOrEmpty(s))
        {
            result = null;
            return false;
        }

        var parts = s
            .Replace(" ", string.Empty)
            .ToUpperInvariant()
            .Split('+');

        var keystroke = (Keys: Keys.None, Modifiers: KeyModifiers.None);

        foreach (var part in parts)
        {
            switch (part)
            {
                case "CTRL":
                    keystroke.Modifiers |= KeyModifiers.Control;
                    break;
                case "ALT":
                    keystroke.Modifiers |= KeyModifiers.Alt;
                    break;
                case "SHIFT":
                    keystroke.Modifiers |= KeyModifiers.Shift;
                    break;
                case "WIN":
                    keystroke.Modifiers |= KeyModifiers.Windows;
                    break;
                default:
                    if (!Enum.TryParse<Keys>(part, out var key))
                    {
                        result = null;
                        return false;
                    }

                    keystroke.Keys = key;
                    break;
            }
        }

        result = new Keystroke(
            keystroke.Keys, keystroke.Modifiers);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (this.Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("CTRL");
        }

        if (this.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("ALT");
        }

        if (this.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("SHIFT");
        }

        if (this.Modifiers.HasFlag(KeyModifiers.Windows))
        {
            parts.Add("WIN");
        }

        if (this.Key != Keys.None)
        {
            parts.Add(this.Key.ToString());
        }

        return string.Join(" + ", parts);
    }
}
