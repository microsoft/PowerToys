// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerOcrProperties
    {
        public PowerOcrProperties()
        {
            ActivationShortcut = new HotkeySettings(true, false, false, true, 0x54); // Win+Shift+T
            PreferredLanguage = string.Empty;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public string PreferredLanguage { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
