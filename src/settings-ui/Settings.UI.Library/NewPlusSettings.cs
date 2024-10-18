// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Settings.UI.Library.Resources;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class NewPlusSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "NewPlus";
        public const string ModuleVersion = "1.0";

        [JsonPropertyName("properties")]
        public NewPlusProperties Properties { get; set; }

        public NewPlusSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new NewPlusProperties();
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
