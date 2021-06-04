// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AwakeProperties
    {
        public AwakeProperties()
        {
            KeepDisplayOn = false;
            Mode = AwakeMode.PASSIVE;
            Hours = 0;
            Minutes = 0;
        }

        [JsonPropertyName("awake_keep_display_on")]
        public bool KeepDisplayOn { get; set; }

        [JsonPropertyName("awake_mode")]
        public AwakeMode Mode { get; set; }

        [JsonPropertyName("awake_hours")]
        public uint Hours { get; set; }

        [JsonPropertyName("awake_minutes")]
        public uint Minutes { get; set; }
    }

    public enum AwakeMode
    {
        PASSIVE = 0,
        INDEFINITE = 1,
        TIMED = 2,
    }
}
