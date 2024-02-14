// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerAccentSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "QuickAccent";
        public const string ModuleVersion = "0.0.1";
        public const int DefaultInputTimeMs = 300; // PowerAccentKeyboardService.PowerAccentSettings.inputTime should be the same

        [JsonPropertyName("properties")]
        public PowerAccentProperties Properties { get; set; }

        public PowerAccentSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new PowerAccentProperties();
        }

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
