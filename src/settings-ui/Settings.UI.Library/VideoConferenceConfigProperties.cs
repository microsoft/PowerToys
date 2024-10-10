// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class VideoConferenceConfigProperties
    {
        [CmdConfigureIgnoreAttribute]
        public HotkeySettings DefaultMuteCameraAndMicrophoneHotkey => new HotkeySettings()
        {
            Win = true,
            Ctrl = false,
            Alt = false,
            Shift = true,
            Key = "Q",
            Code = 81,
        };

        [CmdConfigureIgnoreAttribute]
        public HotkeySettings DefaultMuteMicrophoneHotkey => new HotkeySettings()
        {
            Win = true,
            Ctrl = false,
            Alt = false,
            Shift = true,
            Key = "A",
            Code = 65,
        };

        [CmdConfigureIgnoreAttribute]
        public HotkeySettings DefaultPushToTalkMicrophoneHotkey => new HotkeySettings()
        {
            Win = true,
            Ctrl = false,
            Alt = false,
            Shift = true,
            Key = "I",
            Code = 73,
        };

        [CmdConfigureIgnoreAttribute]
        public HotkeySettings DefaultMuteCameraHotkey => new HotkeySettings()
        {
            Win = true,
            Ctrl = false,
            Alt = false,
            Shift = true,
            Key = "O",
            Code = 79,
        };

        public VideoConferenceConfigProperties()
        {
            MuteCameraAndMicrophoneHotkey = new KeyboardKeysProperty(DefaultMuteCameraAndMicrophoneHotkey);
            MuteMicrophoneHotkey = new KeyboardKeysProperty(DefaultMuteMicrophoneHotkey);
            PushToTalkMicrophoneHotkey = new KeyboardKeysProperty(DefaultPushToTalkMicrophoneHotkey);
            MuteCameraHotkey = new KeyboardKeysProperty(DefaultMuteCameraHotkey);

            PushToReverseEnabled = new BoolProperty(false);
        }

        [JsonPropertyName("mute_camera_and_microphone_hotkey")]
        public KeyboardKeysProperty MuteCameraAndMicrophoneHotkey { get; set; }

        [JsonPropertyName("mute_microphone_hotkey")]
        public KeyboardKeysProperty MuteMicrophoneHotkey { get; set; }

        [JsonPropertyName("push_to_talk_microphone_hotkey")]
        public KeyboardKeysProperty PushToTalkMicrophoneHotkey { get; set; }

        [JsonPropertyName("push_to_reverse_enabled")]
        public BoolProperty PushToReverseEnabled { get; set; }

        [JsonPropertyName("mute_camera_hotkey")]
        public KeyboardKeysProperty MuteCameraHotkey { get; set; }

        [JsonPropertyName("selected_camera")]
        public StringProperty SelectedCamera { get; set; } = string.Empty;

        [JsonPropertyName("selected_mic")]
        public StringProperty SelectedMicrophone { get; set; } = string.Empty;

        [JsonPropertyName("toolbar_position")]
        public StringProperty ToolbarPosition { get; set; } = "Top right corner";

        [JsonPropertyName("toolbar_monitor")]
        public StringProperty ToolbarMonitor { get; set; } = "Main monitor";

        [JsonPropertyName("camera_overlay_image_path")]
        public StringProperty CameraOverlayImagePath { get; set; } = string.Empty;

        [JsonPropertyName("theme")]
        public StringProperty Theme { get; set; }

        [JsonPropertyName("toolbar_hide")]
        public StringProperty ToolbarHide { get; set; } = "When both camera and microphone are unmuted";

        [JsonPropertyName("startup_action")]
        public StringProperty StartupAction { get; set; } = "Nothing";

        // converts the current to a json string.
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
