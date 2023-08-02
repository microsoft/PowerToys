// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class RegistryPreviewSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "RegistryPreview";

        [JsonPropertyName("properties")]
        public RegistryPreviewProperties Properties { get; set; }

        public RegistryPreviewSettings()
        {
            Properties = new RegistryPreviewProperties();
            Version = "1";
            Name = ModuleName;
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
