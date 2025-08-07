// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Needs to be kept in sync with src\modules\alwaysontop\AlwaysOnTop\Settings.h
    public class AlwaysOnTopProperties
    {
        public static readonly HotkeySettings DefaultHotkeyValue = new HotkeySettings(true, true, false, false, 0x54);
        public const bool DefaultFrameEnabled = true;
        public const int DefaultFrameThickness = 15;
        public const string DefaultFrameColor = "#0099cc";
        public const bool DefaultFrameAccentColor = true;
        public const int DefaultFrameOpacity = 100;
        public const bool DefaultSoundEnabled = true;
        public const bool DefaultDoNotActivateOnGameMode = true;
        public const bool DefaultRoundCornersEnabled = true;

        public AlwaysOnTopProperties()
        {
            Hotkey = new KeyboardKeysProperty(DefaultHotkeyValue);
            FrameEnabled = new BoolProperty(DefaultFrameEnabled);
            FrameThickness = new IntProperty(DefaultFrameThickness);
            FrameColor = new StringProperty(DefaultFrameColor);
            FrameAccentColor = new BoolProperty(DefaultFrameAccentColor);
            FrameOpacity = new IntProperty(DefaultFrameOpacity);
            SoundEnabled = new BoolProperty(DefaultSoundEnabled);
            DoNotActivateOnGameMode = new BoolProperty(DefaultDoNotActivateOnGameMode);
            RoundCornersEnabled = new BoolProperty(DefaultRoundCornersEnabled);
            ExcludedApps = new StringProperty();
        }

        [JsonPropertyName("hotkey")]
        public KeyboardKeysProperty Hotkey { get; set; }

        [JsonPropertyName("frame-enabled")]
        public BoolProperty FrameEnabled { get; set; }

        [JsonPropertyName("frame-thickness")]
        public IntProperty FrameThickness { get; set; }

        [JsonPropertyName("frame-color")]
        public StringProperty FrameColor { get; set; }

        [JsonPropertyName("frame-opacity")]
        public IntProperty FrameOpacity { get; set; }

        [JsonPropertyName("frame-accent-color")]
        public BoolProperty FrameAccentColor { get; set; }

        [JsonPropertyName("sound-enabled")]
        public BoolProperty SoundEnabled { get; set; }

        [JsonPropertyName("do-not-activate-on-game-mode")]
        public BoolProperty DoNotActivateOnGameMode { get; set; }

        [JsonPropertyName("excluded-apps")]
        public StringProperty ExcludedApps { get; set; }

        [JsonPropertyName("round-corners-enabled")]
        public BoolProperty RoundCornersEnabled { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
