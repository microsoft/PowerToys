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

        public CursorWrapProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            AutoActivate = new BoolProperty(false);
            DisableWrapDuringDrag = new BoolProperty(true);
        }
    }
}
