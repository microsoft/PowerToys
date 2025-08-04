// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class DarkModeProperties
    {
        public DarkModeProperties()
        {
            ChangeSystem = false;
            ChangeApps = false;
            UseLocation = false;
            LightTime = 0;
            DarkTime = 1;
            Latitude = 0;
            Longitude = 0;
        }

        [JsonPropertyName("changeSystem")]
        public bool ChangeSystem { get; set; }

        [JsonPropertyName("changeApps")]
        public bool ChangeApps { get; set; }

        [JsonPropertyName("useLocation")]
        public bool UseLocation { get; set; }

        [JsonPropertyName("lightTime")]
        public uint LightTime { get; set; }

        [JsonPropertyName("darkTime")]
        public uint DarkTime { get; set; }

        [JsonPropertyName("latitude")]
        public uint Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public uint Longitude { get; set; }
    }
}
