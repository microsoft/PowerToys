// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScreencastModeSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ScreencastMode";
        public const string ModuleVersion = "0.0.2";

        [JsonPropertyName("properties")]
        public ScreencastModeProperties Properties { get; set; }

        public ScreencastModeSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new ScreencastModeProperties();
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
