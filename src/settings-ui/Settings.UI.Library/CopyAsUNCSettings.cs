// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CopyAsUNCSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Copy as UNC";
        public const string ModuleVersion = "1";

        [JsonPropertyName("properties")]
        public CopyAsUNCProperties Properties { get; set; }

        public CopyAsUNCSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new CopyAsUNCProperties();
        }

        public CopyAsUNCSettings(CopyAsUNCLocalProperties localProperties)
        {
            ArgumentNullException.ThrowIfNull(localProperties);

            Properties = new CopyAsUNCProperties();
            Properties.ExtendedContextMenuOnly.Value = localProperties.ExtendedContextMenuOnly;
            Version = ModuleVersion;
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
