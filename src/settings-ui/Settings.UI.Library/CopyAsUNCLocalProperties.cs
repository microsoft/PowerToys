// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CopyAsUNCLocalProperties : ISettingsConfig
    {
        public CopyAsUNCLocalProperties()
        {
            ExtendedContextMenuOnly = false;
        }

        [JsonPropertyName("showInExtendedContextMenu")]
        public bool ExtendedContextMenuOnly { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this, SettingsSerializationContext.Default.CopyAsUNCLocalProperties);
        }

        public string GetModuleName()
        {
            return CopyAsUNCSettings.ModuleName;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
