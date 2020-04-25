// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class HotkeySettings
    {
        public HotkeySettings()
        {
            this.Win = false;
            this.Ctrl = false;
            this.Alt = false;
            this.Shift = false;
            this.Key = string.Empty;
            this.Code = 0;
        }

        [JsonPropertyName("win")]
        public bool Win { get; set; }

        [JsonPropertyName("ctrl")]
        public bool Ctrl { get; set; }

        [JsonPropertyName("alt")]
        public bool Alt { get; set; }

        [JsonPropertyName("shift")]
        public bool Shift { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

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

            output.Append(Key);
            return output.ToString();
        }

        public bool IsValid()
        {
            return (Alt || Ctrl || Win || Shift) && Code != 0;
        }

        public bool IsEmpty()
        {
            return !Alt && !Ctrl && !Win && !Shift && Code == 0;
        }
    }
}
