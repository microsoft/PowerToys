// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerLauncherSettings : BasePTModuleSettings
    {
        private readonly ISettingsUtils _settingsUtils;
        public const string ModuleName = "PowerToys Run";

        [JsonPropertyName("properties")]
        public PowerLauncherProperties Properties { get; set; }

        public PowerLauncherSettings(ISettingsUtils settingsUtils)
        {
            Properties = new PowerLauncherProperties();
            Version = "1";
            Name = ModuleName;
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
        }

        public virtual void Save()
        {
            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            _settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }
    }
}
