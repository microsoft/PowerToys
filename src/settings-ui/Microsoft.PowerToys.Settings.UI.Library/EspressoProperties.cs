// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class EspressoProperties
    {
        public EspressoProperties()
        {
            KeepDisplayOn = new BoolProperty();
            Mode = EspressoMode.INDEFINITE;
            Hours = new IntProperty();
            Minutes = new IntProperty();
        }

        [JsonPropertyName("espresso_keep_display_on")]
        public BoolProperty KeepDisplayOn { get; set; }

        [JsonPropertyName("espresso_mode")]
        public EspressoMode Mode { get; set; }

        [JsonPropertyName("espresso_hours")]
        public IntProperty Hours { get; set; }

        [JsonPropertyName("espresso_minutes")]
        public IntProperty Minutes { get; set; }
    }

    public enum EspressoMode
    {
        INDEFINITE = 0,
        TIMED = 1,
    }
}
