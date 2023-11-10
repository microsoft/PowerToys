// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FindMyMouseSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "FindMyMouse";

        [JsonPropertyName("properties")]
        public FindMyMouseProperties Properties { get; set; }

        public FindMyMouseSettings()
        {
            Name = ModuleName;
            Properties = new FindMyMouseProperties();
            Version = "1.1";
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            if (Version == "1.0")
            {
                if (Properties.ActivationMethod.Value == 1)
                {
                    Properties.ActivationMethod = new IntProperty(2);
                }

                Version = "1.1";
                return true;
            }

            return false;
        }
    }
}
