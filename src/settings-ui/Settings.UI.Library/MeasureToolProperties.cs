// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MeasureToolProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, true, false, true, 0x4D);

        public MeasureToolProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            UnitsOfMeasure = new IntProperty(0);
            PixelTolerance = new IntProperty(30);
            ContinuousCapture = false;
            DrawFeetOnCross = true;
            PerColorChannelEdgeDetection = false;
            MeasureCrossColor = new StringProperty("#FF4500");
            DefaultMeasureStyle = new IntProperty((int)MeasureToolMeasureStyle.None);
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ContinuousCapture { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool DrawFeetOnCross { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool PerColorChannelEdgeDetection { get; set; }

        [CmdConfigureIgnore]
        public IntProperty UnitsOfMeasure { get; set; }

        public IntProperty PixelTolerance { get; set; }

        public StringProperty MeasureCrossColor { get; set; }

        public IntProperty DefaultMeasureStyle { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}
