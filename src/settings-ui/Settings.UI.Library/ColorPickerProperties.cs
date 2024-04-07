// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorPickerProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x43);

        public ColorPickerProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            ChangeCursor = false;
            ColorHistoryLimit = 20;
            VisibleColorFormats = new Dictionary<string, KeyValuePair<bool, string>>();
            VisibleColorFormats.Add("HEX", new KeyValuePair<bool, string>(true, ColorFormatHelper.GetDefaultFormat("HEX")));
            VisibleColorFormats.Add("RGB", new KeyValuePair<bool, string>(true, ColorFormatHelper.GetDefaultFormat("RGB")));
            VisibleColorFormats.Add("HSL", new KeyValuePair<bool, string>(true, ColorFormatHelper.GetDefaultFormat("HSL")));
            VisibleColorFormats.Add("HSV", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("HSV")));
            VisibleColorFormats.Add("CMYK", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("CMYK")));
            VisibleColorFormats.Add("HSB", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("HSB")));
            VisibleColorFormats.Add("HSI", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("HSI")));
            VisibleColorFormats.Add("HWB", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("HWB")));
            VisibleColorFormats.Add("NCol", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("NCol")));
            VisibleColorFormats.Add("CIELAB", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("CIELAB")));
            VisibleColorFormats.Add("CIEXYZ", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("CIEXYZ")));
            VisibleColorFormats.Add("VEC4", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("VEC4")));
            VisibleColorFormats.Add("Decimal", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("Decimal")));
            VisibleColorFormats.Add("HEX Int", new KeyValuePair<bool, string>(false, ColorFormatHelper.GetDefaultFormat("HEX Int")));
            ShowColorName = false;
            ActivationAction = ColorPickerActivationAction.OpenColorPickerAndThenEditor;
            CopiedColorRepresentation = "HEX";
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("changecursor")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        [CmdConfigureIgnoreAttribute]
        public bool ChangeCursor { get; set; }

        [JsonPropertyName("copiedcolorrepresentation")]
        public string CopiedColorRepresentation { get; set; }

        [JsonPropertyName("activationaction")]
        public ColorPickerActivationAction ActivationAction { get; set; }

        // Property ColorHistory is not used, the color history is saved separately in the colorHistory.json file
        [JsonPropertyName("colorhistory")]
        [CmdConfigureIgnoreAttribute]
        public List<string> ColorHistory { get; set; }

        [JsonPropertyName("colorhistorylimit")]
        [CmdConfigureIgnoreAttribute]
        public int ColorHistoryLimit { get; set; }

        [JsonPropertyName("visiblecolorformats")]
        [CmdConfigureIgnoreAttribute]
        public Dictionary<string, KeyValuePair<bool, string>> VisibleColorFormats { get; set; }

        [JsonPropertyName("showcolorname")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowColorName { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
