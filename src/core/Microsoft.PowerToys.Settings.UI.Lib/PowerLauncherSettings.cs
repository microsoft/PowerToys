// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerLauncherSettings : BasePTModuleSettings
    {
        public const string POWERTOYNAME = "PowerToys Run";

        public PowerLauncherProperties properties { get; set; }

        public PowerLauncherSettings()
        {
            properties = new PowerLauncherProperties();
            version = "1";
            name = POWERTOYNAME;
        }

        public virtual void Save()
        {
            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            SettingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), POWERTOYNAME);
        }
    }
}
