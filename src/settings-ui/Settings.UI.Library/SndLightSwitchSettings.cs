// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndLightSwitchSettings
    {
        [JsonPropertyName("LightSwitch")]
        public LightSwitchSettings Settings { get; set; }

        public SndLightSwitchSettings()
        {
        }

        public SndLightSwitchSettings(LightSwitchSettings settings)
        {
            Settings = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
