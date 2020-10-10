// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ColorPickerSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ColorPicker";

        [JsonPropertyName("properties")]
        public ColorPickerProperties Properties { get; set; }

        public ColorPickerSettings()
        {
            Properties = new ColorPickerProperties();
            Version = "1";
            Name = ModuleName;
        }

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            settingsUtils.SaveSettings(this, ModuleName);
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
    }
}
