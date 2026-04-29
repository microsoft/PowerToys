// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Settings.UI.Library
{
    public class LastVersionRunSettings : ISettingsConfig
    {
        [JsonPropertyName("last_version")]
        public string LastVersion { get; set; }

        public string GetModuleName()
        {
            return "LastVersionRun";
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
