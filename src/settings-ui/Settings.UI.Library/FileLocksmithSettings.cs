// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FileLocksmithSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "File Locksmith";
        public const string ModuleVersion = "1";

        [JsonPropertyName("properties")]
        public FileLocksmithProperties Properties { get; set; }

        public FileLocksmithSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new FileLocksmithProperties();
        }

        public FileLocksmithSettings(FileLocksmithLocalProperties localProperties)
        {
            ArgumentNullException.ThrowIfNull(localProperties);

            Properties = new FileLocksmithProperties();
            Properties.ExtendedContextMenuOnly.Value = localProperties.ExtendedContextMenuOnly;
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
