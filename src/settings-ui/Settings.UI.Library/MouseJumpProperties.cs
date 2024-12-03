// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseJumpProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x44);

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut
        {
            get;
            set;
        }

        [JsonPropertyName("thumbnail_size")]
        public MouseJumpThumbnailSize ThumbnailSize
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the preview type.
        /// Allowed values are "compact", "bezelled", "custom"
        /// </summary>
        [JsonPropertyName("preview_type")]
        public string PreviewType
        {
            get;
            set;
        }

        [JsonPropertyName("background_color_1")]
        public string BackgroundColor1
        {
            get;
            set;
        }

        [JsonPropertyName("background_color_2")]
        public string BackgroundColor2
        {
            get;
            set;
        }

        [JsonPropertyName("border_thickness")]
        public int BorderThickness
        {
            get;
            set;
        }

        [JsonPropertyName("border_color")]
        public string BorderColor
        {
            get;
            set;
        }

        [JsonPropertyName("border_3d_depth")]
        public int Border3dDepth
        {
            get;
            set;
        }

        [JsonPropertyName("border_padding")]
        public int BorderPadding
        {
            get;
            set;
        }

        [JsonPropertyName("bezel_thickness")]
        public int BezelThickness
        {
            get;
            set;
        }

        [JsonPropertyName("bezel_color")]
        public string BezelColor
        {
            get;
            set;
        }

        [JsonPropertyName("bezel_3d_depth")]
        public int Bezel3dDepth
        {
            get;
            set;
        }

        [JsonPropertyName("screen_margin")]
        public int ScreenMargin
        {
            get;
            set;
        }

        [JsonPropertyName("screen_color_1")]
        public string ScreenColor1
        {
            get;
            set;
        }

        [JsonPropertyName("screen_color_2")]
        public string ScreenColor2
        {
            get;
            set;
        }

        public MouseJumpProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            ThumbnailSize = new MouseJumpThumbnailSize();
        }
    }
}
