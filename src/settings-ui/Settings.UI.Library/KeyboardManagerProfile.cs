// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeyboardManagerProfile : ISettingsConfig
    {
        [JsonPropertyName("remapKeys")]
        public RemapKeysDataModel RemapKeys { get; set; }

        [JsonPropertyName("remapKeysToText")]
        public RemapKeysDataModel RemapKeysToText { get; set; }

        [JsonPropertyName("remapShortcuts")]
        public ShortcutsKeyDataModel RemapShortcuts { get; set; }

        [JsonPropertyName("remapShortcutsToText")]
        public ShortcutsKeyDataModel RemapShortcutsToText { get; set; }

        public KeyboardManagerProfile()
        {
            RemapKeys = new RemapKeysDataModel();
            RemapKeysToText = new RemapKeysDataModel();

            RemapShortcuts = new ShortcutsKeyDataModel();
            RemapShortcutsToText = new ShortcutsKeyDataModel();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public string GetModuleName()
        {
            return KeyboardManagerSettings.ModuleName;
        }

        // This can be utilized in the future if the default.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
