// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class HotkeySettings
    {
        public bool win { get; set; }

        public bool ctrl { get; set; }

        public bool alt { get; set; }

        public bool shift { get; set; }

        public string key { get; set; }

        public int code { get; set; }

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();

            if (win)
            {
                output.Append("Win + ");
            }

            if (ctrl)
            {
                output.Append("Ctrl + ");
            }

            if (alt)
            {
                output.Append("Alt + ");
            }

            if (shift)
            {
                output.Append("Shift + ");
            }

            output.Append(key);
            return output.ToString();
        }
    }
}
