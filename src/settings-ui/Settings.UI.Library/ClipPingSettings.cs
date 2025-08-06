// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ClipPingSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ClipPing";
        public const string ModuleVersion = "0.0.1";

        public ClipPingSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new ClipPingProperties();
        }

        [JsonPropertyName("properties")]
        public ClipPingProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
