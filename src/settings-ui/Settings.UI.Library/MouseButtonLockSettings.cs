// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseButtonLockSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "MouseButtonLock";

        [JsonPropertyName("properties")]
        public MouseButtonLockProperties Properties { get; set; }

        public MouseButtonLockSettings()
        {
            Name = ModuleName;
            Properties = new MouseButtonLockProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.MouseButtonLock;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
