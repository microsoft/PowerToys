// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AltWindowCycleProperties
    {
        // Alt+` cycles to the next window of the focused app.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultNextWindowShortcut => new HotkeySettings(false, false, true, false, 0xC0);

        // Shift+Alt+` cycles to the previous window of the focused app.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPreviousWindowShortcut => new HotkeySettings(false, false, true, true, 0xC0);

        [JsonPropertyName("next_window_shortcut")]
        public HotkeySettings NextWindowShortcut { get; set; }

        [JsonPropertyName("previous_window_shortcut")]
        public HotkeySettings PreviousWindowShortcut { get; set; }

        public AltWindowCycleProperties()
        {
            NextWindowShortcut = DefaultNextWindowShortcut;
            PreviousWindowShortcut = DefaultPreviousWindowShortcut;
        }
    }
}
