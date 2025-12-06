// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeystrokeOverlayUI.Controls
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KeystrokeEvent
    {
        public uint VirtualKey;
        public bool IsPressed;
        public List<string> Modifiers;
        public string Text;
        public string EventType;

        // Convert the KeystrokeEvent to a human-readable string for display
        public override string ToString()
        {
            if (!IsPressed)
            {
                return string.Empty;
            }

            bool isCharEvent = string.Equals(EventType, "char", StringComparison.OrdinalIgnoreCase);
            string keyName = null;

            // check for modifiers
            bool hasCtrl = Modifiers != null && Modifiers.Contains("Ctrl");
            bool hasAlt = Modifiers != null && Modifiers.Contains("Alt");
            bool hasWin = Modifiers != null && Modifiers.Contains("Win");

            if (isCharEvent)
            {
                if (!string.IsNullOrEmpty(Text))
                {
                    keyName = Text;
                }
            }
            else
            {
                if (IsCommandKey(VirtualKey) || hasCtrl || hasAlt || hasWin)
                {
                    keyName = GetKeyName(VirtualKey);
                }
            }

            // only register valid key combinations
            if (keyName != null)
            {
                var displayParts = new List<string>();

                if (Modifiers != null)
                {
                    foreach (var mod in Modifiers)
                    {
                        // Don't show "Shift" if we are displaying a Char
                        // (because the Char "!" already implies Shift was pressed)
                        if (isCharEvent && mod == "Shift")
                        {
                            continue;
                        }

                        displayParts.Add(GetModifierSymbol(mod));
                    }
                }

                // Avoid duplicates (e.g. Ctrl + Ctrl)
                string modSym = GetModifierSymbol(keyName);
                if (!displayParts.Contains(keyName) && !displayParts.Contains(modSym))
                {
                    displayParts.Add(keyName);
                }

                return string.Join(" + ", displayParts);
            }

            return string.Empty;
        }

        private static bool IsCommandKey(uint virtualKey)
        {
            var key = (Windows.System.VirtualKey)virtualKey;

            switch (key)
            {
                case Windows.System.VirtualKey.Space:
                case Windows.System.VirtualKey.Enter:
                case Windows.System.VirtualKey.Tab:
                case Windows.System.VirtualKey.Back:
                case Windows.System.VirtualKey.Escape:
                case Windows.System.VirtualKey.Delete:
                case Windows.System.VirtualKey.Insert:
                case Windows.System.VirtualKey.Home:
                case Windows.System.VirtualKey.End:
                case Windows.System.VirtualKey.PageUp:
                case Windows.System.VirtualKey.PageDown:
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.Up:
                case Windows.System.VirtualKey.Down:
                case Windows.System.VirtualKey.Snapshot: // Print Screen
                case Windows.System.VirtualKey.Pause:
                case Windows.System.VirtualKey.CapitalLock:
                case Windows.System.VirtualKey.LeftWindows:
                case Windows.System.VirtualKey.RightWindows:
                    return true;
            }

            if (virtualKey >= 112 && virtualKey <= 135)
            {
                return true;
            }

            // BLOCK everything else (A-Z, 0-9, Punctuation)
            // These will be handled by the "Char" event instead
            return false;
        }

        private static string GetModifierSymbol(string modifier)
        {
            return modifier switch
            {
                "Ctrl" => "Ctrl",
                "Alt" => "Alt",
                "Shift" => "⇧",
                "Win" => "⊞",
                _ => modifier,
            };
        }

        private static string GetKeyName(uint virtualKey)
        {
            var key = (Windows.System.VirtualKey)virtualKey;
            int intKey = (int)virtualKey;

            switch (key)
            {
                case Windows.System.VirtualKey.Space: return "Space";
                case Windows.System.VirtualKey.Enter: return "Enter";
                case Windows.System.VirtualKey.Back: return "Backspace";
                case Windows.System.VirtualKey.Tab: return "Tab";
                case Windows.System.VirtualKey.Escape: return "Esc";
                case Windows.System.VirtualKey.Delete: return "Del";
                case Windows.System.VirtualKey.Insert: return "Ins";
                case Windows.System.VirtualKey.Left: return "←";
                case Windows.System.VirtualKey.Right: return "→";
                case Windows.System.VirtualKey.Up: return "↑";
                case Windows.System.VirtualKey.Down: return "↓";
                case Windows.System.VirtualKey.LeftWindows: return "⊞";
                case Windows.System.VirtualKey.RightWindows: return "⊞";
            }

            // Handle Letters (A-Z)
            if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
            {
                return key.ToString();
            }

            // Handle Numbers (0-9) - strip the word "Number"
            if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
            {
                return key.ToString().Replace("Number", string.Empty);
            }

            // Handle Numpad
            if (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9)
            {
                return "Num " + key.ToString().Replace("NumberPad", string.Empty);
            }

            // Handle Punctuation (The "Numbers" you are seeing)
            switch (intKey)
            {
                case 186: return ";";
                case 187: return "=";
                case 188: return ",";
                case 189: return "-";
                case 190: return ".";
                case 191: return "/"; // Forward Slash
                case 192: return "`"; // Backtick
                case 219: return "[";
                case 220: return "\\"; // Backslash
                case 221: return "]";
                case 222: return "'"; // Single Quote
                case 173: return "Mute";
                case 174: return "Vol -";
                case 175: return "Vol +";
                case 176: return "Next";
                case 177: return "Prev";
                case 179: return "Play/Pause";
            }

            return key.ToString();
        }
    }
}
