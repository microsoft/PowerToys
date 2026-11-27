// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SoundManagerProperties
    {
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new(true, true, false, true, 0x56); // Win+Ctrl+Shift+V

        public SoundManagerProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            MuteShortcuts = new();
        }

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        // The programs that have a shortcut of their own, which silence them.
        [JsonPropertyName("mute-shortcuts")]
        public SoundManagerMuteShortcuts MuteShortcuts { get; set; }
    }
}
