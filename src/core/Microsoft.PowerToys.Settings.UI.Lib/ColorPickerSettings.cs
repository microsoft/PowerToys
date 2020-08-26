// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ColorPickerSettings : BasePTModuleSettings
    {
        public const string ModuleName = "ColorPicker";

        [JsonPropertyName("properties")]
        public ColorPickerProperties Properties { get; set; }

        private readonly SettingsUtils _settingsUtils;

        public ColorPickerSettings()
        {
            Properties = new ColorPickerProperties();
            Version = "1";
            Name = ModuleName;
            _settingsUtils = new SettingsUtils(new SystemIOProvider());
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
