// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class WindowTabsProperties
    {
        // Win+Ctrl+PageDown selects the next tab.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultNextTabShortcut => new HotkeySettings(true, true, false, false, 0x22);

        // Win+Ctrl+PageUp selects the previous tab.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPreviousTabShortcut => new HotkeySettings(true, true, false, false, 0x21);

        [JsonPropertyName("restore_session")]
        public BoolProperty RestoreSession { get; set; } = new BoolProperty(true);

        private HotkeySettings _nextWindowShortcut;

        private HotkeySettings _previousWindowShortcut;

        [JsonPropertyName("next_tab_shortcut")]
        public HotkeySettings NextTabShortcut
        {
            get => _nextWindowShortcut ?? DefaultNextTabShortcut;
            set => _nextWindowShortcut = value;
        }

        [JsonPropertyName("previous_tab_shortcut")]
        public HotkeySettings PreviousTabShortcut
        {
            get => _previousWindowShortcut ?? DefaultPreviousTabShortcut;
            set => _previousWindowShortcut = value;
        }

        public WindowTabsProperties()
        {
            NextTabShortcut = DefaultNextTabShortcut;
            PreviousTabShortcut = DefaultPreviousTabShortcut;
        }
    }
}
