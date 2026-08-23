// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AltWindowCycleProperties
    {
        public static readonly HotkeySettings DefaultNextWindowShortcutValue = new HotkeySettings(false, false, false, true, 0xC0); // Alt+`
        public static readonly HotkeySettings DefaultPreviousWindowShortcutValue = new HotkeySettings(false, false, true, true, 0xC0); // Alt+Shift+`

        public AltWindowCycleProperties()
        {
            NextWindowShortcut = new KeyboardKeysProperty(DefaultNextWindowShortcutValue);
            PreviousWindowShortcut = new KeyboardKeysProperty(DefaultPreviousWindowShortcutValue);
        }

        [JsonPropertyName("next_window_shortcut")]
        public KeyboardKeysProperty NextWindowShortcut { get; set; }

        [JsonPropertyName("previous_window_shortcut")]
        public KeyboardKeysProperty PreviousWindowShortcut { get; set; }
    }
}
