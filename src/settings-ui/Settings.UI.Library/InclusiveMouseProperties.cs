// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class InclusiveMouseProperties
    {
        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        public InclusiveMouseProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, true, false, 0x50); // Ctrl + Alt + P
        }
    }
}
