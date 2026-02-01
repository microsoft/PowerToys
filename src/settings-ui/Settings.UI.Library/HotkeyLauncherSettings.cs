// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class HotkeyLauncherSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "HotkeyLauncher";

        public HotkeyLauncherSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new HotkeyLauncherProperties();
        }

        [JsonPropertyName("properties")]
        public HotkeyLauncherProperties Properties { get; set; }

        public ModuleType GetModuleType() => ModuleType.HotkeyLauncher;

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
