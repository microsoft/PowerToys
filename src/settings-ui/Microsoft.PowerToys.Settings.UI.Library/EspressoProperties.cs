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
            KeepDisplayOn = false;
            Mode = EspressoMode.PASSIVE;
            Hours = 0;
            Minutes = 0;
        }

        [JsonPropertyName("espresso_keep_display_on")]
        public bool KeepDisplayOn { get; set; }

        [JsonPropertyName("espresso_mode")]
        public EspressoMode Mode { get; set; }

        [JsonPropertyName("espresso_hours")]
        public uint Hours { get; set; }

        [JsonPropertyName("espresso_minutes")]
        public uint Minutes { get; set; }
    }

    public enum EspressoMode
    {
        PASSIVE = 0,
        INDEFINITE = 1,
        TIMED = 2,
    }
}
