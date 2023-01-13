// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Represents a powertoys module settings sent to the runner.
    public class SndModuleSettings<T>
    {
        [JsonPropertyName("powertoys")]
        public T PowertoysSetting { get; set; }

        public SndModuleSettings()
        {
        }

        public SndModuleSettings(T settings)
        {
            PowertoysSetting = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
