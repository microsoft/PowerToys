// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutsKeyDataModel
    {
        [JsonPropertyName("global")]
        public List<KeysDataModel> GlobalRemapShortcuts { get; init; }

        [JsonPropertyName("appSpecific")]
        public List<AppSpecificKeysDataModel> AppSpecificRemapShortcuts { get; init; }

        public ShortcutsKeyDataModel()
        {
            GlobalRemapShortcuts = new List<KeysDataModel>();
            AppSpecificRemapShortcuts = new List<AppSpecificKeysDataModel>();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
