// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class RobocopyUISettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "RobocopyUI";

        [JsonPropertyName("properties")]
        public RobocopyUIProperties Properties { get; set; }

        public RobocopyUISettings()
        {
            Properties = new RobocopyUIProperties();
            Version = "1";
            Name = ModuleName;
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
