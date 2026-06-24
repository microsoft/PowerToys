// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class GrabAndMoveProperties
    {
        public GrabAndMoveProperties()
        {
            ShouldAbsorbAlt = new BoolProperty(true);
            DoNotActivateOnGameMode = new BoolProperty(true);
            ShowGeometry = new BoolProperty(false);
            UseAltResize = new BoolProperty(true);
            ExcludedApps = new StringProperty();
            ModifierKey = new IntProperty(0); // 0 = Alt, 1 = Win
        }

        [JsonPropertyName("modifierKey")]
        public IntProperty ModifierKey { get; set; }

        [JsonPropertyName("shouldAbsorbAlt")]
        public BoolProperty ShouldAbsorbAlt { get; set; }

        [JsonPropertyName("showGeometry")]
        public BoolProperty ShowGeometry { get; set; }

        [JsonPropertyName("useAltResize")]
        public BoolProperty UseAltResize { get; set; }

        [JsonPropertyName("doNotActivateOnGameMode")]
        public BoolProperty DoNotActivateOnGameMode { get; set; }

        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }
    }
}
