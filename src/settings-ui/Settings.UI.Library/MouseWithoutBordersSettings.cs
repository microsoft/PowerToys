// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseWithoutBordersSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "MouseWithoutBorders";

        [JsonPropertyName("properties")]
        public MouseWithoutBordersProperties Properties { get; set; }

        public MouseWithoutBordersSettings()
        {
            Name = ModuleName;
            Properties = new MouseWithoutBordersProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                MaxDepth = 0,
                IncludeFields = true,
            };

            if (settingsUtils == null)
            {
                throw new ArgumentNullException(nameof(settingsUtils));
            }

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }
    }
}
