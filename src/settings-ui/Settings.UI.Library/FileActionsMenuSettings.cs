// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FileActionsMenuSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "File Actions Menu";
        public const string ModuleVersion = "1";

        [JsonPropertyName("properties")]
        public FileActionsMenuProperties Properties { get; set; }

        public FileActionsMenuSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new FileActionsMenuProperties();
        }

        public FileActionsMenuSettings(FileActionsMenuProperties localProperties)
        {
            ArgumentNullException.ThrowIfNull(localProperties);

            Properties = new FileActionsMenuProperties();
            Version = "1";
            Name = ModuleName;
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
