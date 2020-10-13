// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FancyZonesSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "FancyZones";

        public FancyZonesSettings()
        {
            Version = "1.0";
            Name = ModuleName;
            Properties = new FZConfigProperties();
        }

        [JsonPropertyName("properties")]
        public FZConfigProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
