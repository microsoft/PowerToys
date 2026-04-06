// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class WinPosSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "WinPos";

        public WinPosSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new WinPosProperties();
        }

        [JsonPropertyName("properties")]
        public WinPosProperties Properties { get; set; }

        public string GetModuleName() => Name;

        public bool UpgradeSettingsConfiguration() => false;

        public ModuleType GetModuleType() => ModuleType.WinPos;
    }
}
