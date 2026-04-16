// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShowDesktopSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ShowDesktop";
        public const string ModuleVersion = "0.0.1";

        public ShowDesktopSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new ShowDesktopProperties();
        }

        [JsonPropertyName("properties")]
        public ShowDesktopProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.ShowDesktop;

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
