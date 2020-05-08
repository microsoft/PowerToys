// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ShortcutGuideProperties
    {
        public ShortcutGuideProperties()
        {
            OverlayOpacity = new IntProperty(90);
            PressTime = new IntProperty(900);
            Theme = new StringProperty("light");
        }

        [JsonPropertyName("overlay_opacity")]
        public IntProperty OverlayOpacity { get; set; }

        [JsonPropertyName("press_time")]
        public IntProperty PressTime { get; set; }

        [JsonPropertyName("theme")]
        public StringProperty Theme { get; set; }
    }
}
