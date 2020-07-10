using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class VideoConferenceConfigProperties
    {
        public VideoConferenceConfigProperties()
        {
            this.MuteCameraAndMicrophoneHotkey = new KeyBoardKeysProperty(
                new HotkeySettings()
                {
                    Win = true,
                    Ctrl = false,
                    Alt = false,
                    Shift = false,
                    Key = "N",
                    Code = 78,
                });

            this.MuteMicrophoneHotkey = new KeyBoardKeysProperty(
                new HotkeySettings()
                {
                    Win = true,
                    Ctrl = false,
                    Alt = false,
                    Shift = true,
                    Key = "A",
                    Code = 65,
                });

            this.MuteCameraHotkey = new KeyBoardKeysProperty(
            new HotkeySettings()
            {
                Win = true,
                Ctrl = false,
                Alt = false,
                Shift = true,
                Key = "O",
                Code = 79,
            });

            this.OverlayPosition = new StringProperty("Center");
            this.OverlayMonitor = new StringProperty("Main monitor");
            this.SelectedCamera = new StringProperty("None");
        }

        [JsonPropertyName("mute_camera_and_microphone_hotkey")]
        public KeyBoardKeysProperty MuteCameraAndMicrophoneHotkey { get; set; }

        [JsonPropertyName("mute_microphone_hotkey")]
        public KeyBoardKeysProperty MuteMicrophoneHotkey { get; set; }

        [JsonPropertyName("mute_camera_hotkey")]
        public KeyBoardKeysProperty MuteCameraHotkey { get; set; }

        [JsonPropertyName("selected_camera")]
        public StringProperty SelectedCamera { get; set; }

        [JsonPropertyName("overlay_position")]
        public StringProperty OverlayPosition { get; set; }

        [JsonPropertyName("overlay_monitor")]
        public StringProperty OverlayMonitor { get; set; }

        // converts the current to a json string.
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
