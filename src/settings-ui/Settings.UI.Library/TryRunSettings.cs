// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class TryRunSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "TryRun";

        public TryRunSettings()
        {
            Name = ModuleName;
            Version = "1";
            Properties = new TryRunProperties();
        }

        [JsonPropertyName("properties")]
        public TryRunProperties Properties { get; set; }

        public string GetModuleName() => Name;

        public bool UpgradeSettingsConfiguration() => false;
    }
}
