// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MeasureToolProperties
    {
        public MeasureToolProperties()
        {
            ActivationShortcut = new HotkeySettings(true, false, false, true, 0x4D);
            PixelTolerance = new IntProperty(30);
            ContinuousCapture = true;
            DrawFeetOnCross = true;
            PerColorChannelEdgeDetection = false;
            MeasureCrossColor = new StringProperty("#FF4500");
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ContinuousCapture { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool DrawFeetOnCross { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool PerColorChannelEdgeDetection { get; set; }

        public IntProperty PixelTolerance { get; set; }

        public StringProperty MeasureCrossColor { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}
