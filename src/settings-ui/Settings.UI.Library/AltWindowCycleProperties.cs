// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AltWindowCycleProperties
    {
        // Scan code of the physical key below Esc (` ~ on US layouts). The default shortcuts
        // bind to this key rather than to a fixed virtual-key code, because its VK differs by
        // layout: VK_OEM_3 on US, but VK_OEM_8 on UK where VK_OEM_3 is ' @.
        private const uint DefaultCycleKeyScanCode = 0x29;

        // VK_OEM_3
        private const int FallbackCycleKeyCode = 0xC0;

        // VK_OEM_1 through VK_OEM_102, the layout-dependent character keys.
        private const uint FirstOemKeyCode = 0xBA;
        private const uint LastOemKeyCode = 0xE2;

        // Alt+` cycles to the next window of the focused app.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultNextWindowShortcut => new HotkeySettings(false, false, true, false, GetDefaultCycleKeyCode());

        // Shift+Alt+` cycles to the previous window of the focused app.
        [JsonIgnore]
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPreviousWindowShortcut => new HotkeySettings(false, false, true, true, GetDefaultCycleKeyCode());

        private HotkeySettings _nextWindowShortcut;

        private HotkeySettings _previousWindowShortcut;

        [JsonPropertyName("next_window_shortcut")]
        public HotkeySettings NextWindowShortcut
        {
            get => _nextWindowShortcut ?? DefaultNextWindowShortcut;
            set => _nextWindowShortcut = value;
        }

        [JsonPropertyName("previous_window_shortcut")]
        public HotkeySettings PreviousWindowShortcut
        {
            get => _previousWindowShortcut ?? DefaultPreviousWindowShortcut;
            set => _previousWindowShortcut = value;
        }

        public AltWindowCycleProperties()
        {
            NextWindowShortcut = DefaultNextWindowShortcut;
            PreviousWindowShortcut = DefaultPreviousWindowShortcut;
        }

        // The VK of the key below Esc on the current keyboard layout.
        public static int GetDefaultCycleKeyCode()
        {
            return GetDefaultCycleKeyCode(NativeMethods.MapVirtualKeyEx(DefaultCycleKeyScanCode, NativeMethods.MAPVK_VSC_TO_VK, NativeMethods.GetKeyboardLayout(0)));
        }

        // Falls back to VK_OEM_3 when the layout has no OEM character key below Esc, e.g. on
        // Japanese layouts, where that key is the half-width/full-width IME toggle (VK_OEM_AUTO).
        public static int GetDefaultCycleKeyCode(uint layoutVirtualKey)
        {
            return layoutVirtualKey is >= FirstOemKeyCode and <= LastOemKeyCode ? (int)layoutVirtualKey : FallbackCycleKeyCode;
        }
    }
}
