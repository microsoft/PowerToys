// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Drawing;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V1_0;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump
{
    public class MouseJumpProperties
    {
        public static HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x44);

        public static MouseJumpPreviewStyle DefaultPreviewStyle => new(
            canvasSize: new(
                width: 1600,
                height: 1200
            ),
            canvasStyle: new(
                borderStyle: new(
                    color: SystemColors.Highlight,
                    width: 6,
                    depth: 0
                ),
                paddingStyle: new(
                    width: 4
                ),
                backgroundStyle: new(
                    color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                    color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
                )
            ),
            screenStyle: new(
                marginStyle: new(
                    width: 4
                ),
                borderStyle: new(
                    color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                    width: 12,
                    depth: 4
                ),
                backgroundStyle: new(
                    color1: Color.MidnightBlue,
                    color2: Color.MidnightBlue
                )
            )
        );

        public MouseJumpProperties()
        {
            this.ActivationShortcut = MouseJumpProperties.DefaultActivationShortcut;
#pragma warning disable 0618
            this.ThumbnailSize = null;
#pragma warning restore 0618
            this.PreviewStyle = MouseJumpProperties.DefaultPreviewStyle;
        }

        [JsonConstructor]
        public MouseJumpProperties(HotkeySettings? activationShortcut = null, MouseJumpThumbnailSize? thumbnailSize = null, MouseJumpPreviewStyle? previewStyle = null)
        {
            this.ActivationShortcut = activationShortcut;
#pragma warning disable 0618
            this.ThumbnailSize = thumbnailSize;
#pragma warning restore 0618
            this.PreviewStyle = previewStyle;
        }

        [JsonPropertyName("activation_shortcut")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HotkeySettings? ActivationShortcut
        {
            get;
            set;
        }

        [Obsolete("Obsolete V1.0 config setting - definition left here to support upgrading V1.0 config files.")]
        [JsonPropertyName("thumbnail_size")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MouseJumpThumbnailSize? ThumbnailSize
        {
            get;
            set;
        }

        [JsonPropertyName("preview")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MouseJumpPreviewStyle? PreviewStyle
        {
            get;
            set;
        }
    }
}
