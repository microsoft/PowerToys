// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorPickerSettingsVersion1 : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ColorPicker";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public ColorPickerPropertiesVersion1 Properties { get; set; }

        public ColorPickerSettingsVersion1()
        {
            Properties = new ColorPickerPropertiesVersion1();
            Version = "1";
            Name = ModuleName;
        }

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
