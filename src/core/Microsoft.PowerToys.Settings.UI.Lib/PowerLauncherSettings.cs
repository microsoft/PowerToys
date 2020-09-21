// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerLauncherSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "PowerToys Run";

        [JsonPropertyName("properties")]
        public PowerLauncherProperties Properties { get; set; }

        public PowerLauncherSettings()
        {
            Properties = new PowerLauncherProperties();
            Version = "1.0";
            Name = ModuleName;
        }

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }
    }
}
