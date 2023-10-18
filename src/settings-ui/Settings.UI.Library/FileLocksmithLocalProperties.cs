// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FileLocksmithLocalProperties : ISettingsConfig
    {
        public FileLocksmithLocalProperties()
        {
            ExtendedContextMenuOnly = false;
        }

        [JsonPropertyName("showInExtendedContextMenu")]
        public bool ExtendedContextMenuOnly { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        // This function is required to implement the ISettingsConfig interface and obtain the settings configurations.
        public string GetModuleName()
        {
            string moduleName = FileLocksmithSettings.ModuleName;
            return moduleName;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
