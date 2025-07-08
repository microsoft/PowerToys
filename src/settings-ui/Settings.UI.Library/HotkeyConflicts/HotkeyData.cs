// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class HotkeyData
    {
        public bool Win { get; set; }

        public bool Ctrl { get; set; }

        public bool Shift { get; set; }

        public bool Alt { get; set; }

        public int Key { get; set; }

        public override string ToString()
        {
            var output = new StringBuilder();

            if (Win)
            {
                output.Append("Win + ");
            }

            if (Ctrl)
            {
                output.Append("Ctrl + ");
            }

            if (Alt)
            {
                output.Append("Alt + ");
            }

            if (Shift)
            {
                output.Append("Shift + ");
            }

            if (Key > 0)
            {
                // For virtual key codes, we can display the key name
                // This follows the same pattern as HotkeySettings
                var keyName = GetKeyName((uint)Key);
                output.Append(keyName);
            }
            else if (output.Length >= 2)
            {
                // Remove the trailing " + " if there's no key
                output.Remove(output.Length - 2, 2);
            }

            return output.ToString();
        }

        private static string GetKeyName(uint keyCode)
        {
            // Simple mapping for common virtual key codes
            // This could be extended to use the Helper.GetKeyName method if available
            return keyCode switch
            {
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                0x1B => "Escape",
                0x20 => "Space",
                0x21 => "Page Up",
                0x22 => "Page Down",
                0x23 => "End",
                0x24 => "Home",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0x2D => "Insert",
                0x2E => "Delete",
                >= 0x30 and <= 0x39 => ((char)keyCode).ToString(), // 0-9
                >= 0x41 and <= 0x5A => ((char)keyCode).ToString(), // A-Z
                0x70 => "F1",
                0x71 => "F2",
                0x72 => "F3",
                0x73 => "F4",
                0x74 => "F5",
                0x75 => "F6",
                0x76 => "F7",
                0x77 => "F8",
                0x78 => "F9",
                0x79 => "F10",
                0x7A => "F11",
                0x7B => "F12",
                _ => $"Key{keyCode}",
            };
        }
    }
}
