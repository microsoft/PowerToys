// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyboardManagerProfile : ISettingsConfig
    {
        [JsonPropertyName("remapKeys")]
        public RemapKeysDataModel RemapKeys { get; set; }

        [JsonPropertyName("remapShortcuts")]
        public ShortcutsKeyDataModel RemapShortcuts { get; set; }

        public KeyboardManagerProfile()
        {
            RemapKeys = new RemapKeysDataModel();
            RemapShortcuts = new ShortcutsKeyDataModel();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public string GetModuleName()
        {
            return KeyboardManagerSettings.ModuleName;
        }
    }
}
