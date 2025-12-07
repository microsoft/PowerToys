// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScreencastModeProperties
    {
        public HotkeySettings ScreencastModeShortcut { get; set; }

        [JsonPropertyName("display_position")]
        public StringProperty DisplayPosition { get; set; }

        [JsonPropertyName("text_color")]
        public StringProperty TextColor { get; set; }

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; }

        [JsonPropertyName("text_size")]
        public IntProperty TextSize { get; set; }

        public ScreencastModeProperties()
        {
            ScreencastModeShortcut = new HotkeySettings(true, false, true, false, 83); // Win + Alt + S
            DisplayPosition = new StringProperty("TopRight");
            TextColor = new StringProperty("#FFFFFF");
            BackgroundColor = new StringProperty("#000000");
            TextSize = new IntProperty(18); // Default font size
        }
    }
}
