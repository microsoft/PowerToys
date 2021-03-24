using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class EspressoSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Espresso";
        public const string ModuleVersion = "1.0.0";

        public EspressoSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new EspressoProperties();
        }

        [JsonPropertyName("properties")]
        public EspressoProperties Properties { get; set; }

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
