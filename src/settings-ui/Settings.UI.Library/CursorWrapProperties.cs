// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorWrapProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, true, false, 0x55); // Win + Alt + U

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("auto_activate")]
        public BoolProperty AutoActivate { get; set; }

        [JsonPropertyName("disable_wrap_during_drag")]
        public BoolProperty DisableWrapDuringDrag { get; set; }

        [JsonPropertyName("wrap_mode")]
        public IntProperty WrapMode { get; set; }

        [JsonPropertyName("activation_mode")]
        public IntProperty ActivationMode { get; set; }

        [JsonPropertyName("disable_cursor_wrap_on_single_monitor")]
        public BoolProperty DisableCursorWrapOnSingleMonitor { get; set; }

        public CursorWrapProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            AutoActivate = new BoolProperty(false);
            DisableWrapDuringDrag = new BoolProperty(true);
            WrapMode = new IntProperty(0); // 0=Both (default), 1=VerticalOnly, 2=HorizontalOnly
            ActivationMode = new IntProperty(0); // 0=Always (default), 1=HoldingCtrl, 2=HoldingShift
            DisableCursorWrapOnSingleMonitor = new BoolProperty(false);
        }
    }
}
