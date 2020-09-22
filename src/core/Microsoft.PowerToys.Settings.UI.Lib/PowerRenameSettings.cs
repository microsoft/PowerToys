// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "PowerRename";

        [JsonPropertyName("properties")]
        public PowerRenameProperties Properties { get; set; }

        public PowerRenameSettings()
        {
            Properties = new PowerRenameProperties();
            Version = "1";
            Name = ModuleName;
        }

        public PowerRenameSettings(PowerRenameLocalProperties localProperties)
        {
            Properties = new PowerRenameProperties();
            Properties.PersistState.Value = localProperties.PersistState;
            Properties.MRUEnabled.Value = localProperties.MRUEnabled;
            Properties.MaxMRUSize.Value = localProperties.MaxMRUSize;
            Properties.ShowIcon.Value = localProperties.ShowIcon;
            Properties.ExtendedContextMenuOnly.Value = localProperties.ExtendedContextMenuOnly;

            Version = "1";
            Name = ModuleName;
        }

        public PowerRenameSettings(string ptName)
        {
            Properties = new PowerRenameProperties();
            Version = "1";
            Name = ptName;
        }

        public override string GetSettingsFileName()
        {
            return "power-rename-settings.json";
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the power-rename-settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
