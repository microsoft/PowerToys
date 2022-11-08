// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorPickerProperties
    {
        public ColorPickerProperties()
        {
            ActivationShortcut = new HotkeySettings(true, false, false, true, 0x43);
            ChangeCursor = false;
            ColorHistory = new List<string>();
            ColorHistoryLimit = 20;
            VisibleColorFormats = new Dictionary<string, Tuple<bool, string>>();
            VisibleColorFormats.Add("HEX", Tuple.Create(true, string.Empty));
            VisibleColorFormats.Add("RGB", Tuple.Create(true, string.Empty));
            VisibleColorFormats.Add("HSL", Tuple.Create(true, string.Empty));
            ShowColorName = false;
            ActivationAction = ColorPickerActivationAction.OpenColorPickerAndThenEditor;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("changecursor")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ChangeCursor { get; set; }

        [JsonPropertyName("copiedcolorrepresentation")]
        public ColorRepresentationType CopiedColorRepresentation { get; set; }

        [JsonPropertyName("activationaction")]
        public ColorPickerActivationAction ActivationAction { get; set; }

        [JsonPropertyName("colorhistory")]
        public List<string> ColorHistory { get; set; }

        [JsonPropertyName("colorhistorylimit")]
        public int ColorHistoryLimit { get; set; }

        [JsonPropertyName("visiblecolorformats")]
        public Dictionary<string, Tuple<bool, string>> VisibleColorFormats { get; set; }

        [JsonPropertyName("showcolorname")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowColorName { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
