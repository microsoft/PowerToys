// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class HotkeySettings
    {
        private const int VKTAB = 0x09;

        public HotkeySettings()
        {
            Win = false;
            Ctrl = false;
            Alt = false;
            Shift = false;
            Code = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HotkeySettings"/> class.
        /// </summary>
        /// <param name="win">Should Windows key be used</param>
        /// <param name="ctrl">Should Ctrl key be used</param>
        /// <param name="alt">Should Alt key be used</param>
        /// <param name="shift">Should Shift key be used</param>
        /// <param name="code">Go to https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes to see list of v-keys</param>
        public HotkeySettings(bool win, bool ctrl, bool alt, bool shift, int code)
        {
            Win = win;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
            Code = code;
        }

        public HotkeySettings Clone()
        {
            return new HotkeySettings(Win, Ctrl, Alt, Shift, Code);
        }

        [JsonPropertyName("win")]
        public bool Win { get; set; }

        [JsonPropertyName("ctrl")]
        public bool Ctrl { get; set; }

        [JsonPropertyName("alt")]
        public bool Alt { get; set; }

        [JsonPropertyName("shift")]
        public bool Shift { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

        // This is currently needed for FancyZones, we need to unify these two objects
        // see src\common\settings_objects.h
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();

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

            if (Code > 0)
            {
                var localKey = Helper.GetKeyName((uint)Code);
                output.Append(localKey);
            }
            else if (output.Length >= 2)
            {
                output.Remove(output.Length - 2, 2);
            }

            return output.ToString();
        }

        public bool IsValid()
        {
            if (IsAccessibleShortcut())
            {
                return false;
            }

            return (Alt || Ctrl || Win || Shift) && Code != 0;
        }

        public bool IsEmpty()
        {
            return !Alt && !Ctrl && !Win && !Shift && Code == 0;
        }

        public bool IsAccessibleShortcut()
        {
            // Shift+Tab and Tab are accessible shortcuts
            if ((!Alt && !Ctrl && !Win && Shift && Code == VKTAB)
                || (!Alt && !Ctrl && !Win && !Shift && Code == VKTAB))
            {
                return true;
            }

            return false;
        }
    }
}
