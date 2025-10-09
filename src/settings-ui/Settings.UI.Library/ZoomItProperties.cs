// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ZoomItProperties
    {
        public ZoomItProperties()
        {
        }

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultToggleKey => new HotkeySettings(false, true, false, false, '1'); // Ctrl+1

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultLiveZoomToggleKey => new HotkeySettings(false, true, false, false, '4'); // Ctrl+4

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultDrawToggleKey => new HotkeySettings(false, true, false, false, '2'); // Ctrl+2

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultRecordToggleKey => new HotkeySettings(false, true, false, false, '5'); // Ctrl+5

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultSnipToggleKey => new HotkeySettings(false, true, false, false, '6'); // Ctrl+6

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultBreakTimerKey => new HotkeySettings(false, true, false, false, '3'); // Ctrl+3

        [CmdConfigureIgnore]
        public static HotkeySettings DefaultDemoTypeToggleKey => new HotkeySettings(false, true, false, false, '7'); // Ctrl+7

        public KeyboardKeysProperty ToggleKey { get; set; }

        public KeyboardKeysProperty LiveZoomToggleKey { get; set; }

        public KeyboardKeysProperty DrawToggleKey { get; set; }

        public KeyboardKeysProperty RecordToggleKey { get; set; }

        public KeyboardKeysProperty SnipToggleKey { get; set; }

        public KeyboardKeysProperty BreakTimerKey { get; set; }

        public StringProperty Font { get; set; }

        public KeyboardKeysProperty DemoTypeToggleKey { get; set; }

        public StringProperty DemoTypeFile { get; set; }

        public IntProperty DemoTypeSpeedSlider { get; set; }

        public BoolProperty DemoTypeUserDrivenMode { get; set; }

        public IntProperty BreakTimeout { get; set; }

        public IntProperty BreakOpacity { get; set; }

        public BoolProperty BreakPlaySoundFile { get; set; }

        public StringProperty BreakSoundFile { get; set; }

        public BoolProperty BreakShowBackgroundFile { get; set; }

        public BoolProperty BreakBackgroundStretch { get; set; }

        public StringProperty BreakBackgroundFile { get; set; }

        public IntProperty BreakTimerPosition { get; set; }

        public BoolProperty BreakShowDesktop { get; set; }

        public BoolProperty ShowExpiredTime { get; set; }

        public BoolProperty ShowTrayIcon { get; set; }

        [JsonPropertyName("AnimnateZoom")]
        public BoolProperty AnimateZoom { get; set; }

        public BoolProperty SmoothImage { get; set; }

        public IntProperty ZoominSliderLevel { get; set; }

        public IntProperty RecordScaling { get; set; }

        public BoolProperty CaptureAudio { get; set; }

        public StringProperty MicrophoneDeviceId { get; set; }
    }
}
