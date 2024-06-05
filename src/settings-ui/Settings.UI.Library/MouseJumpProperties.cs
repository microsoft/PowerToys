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
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("thumbnail_size")]
        public MouseJumpThumbnailSize ThumbnailSize { get; set; }

        public MouseJumpProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            ThumbnailSize = new MouseJumpThumbnailSize();
        }
    }
}
