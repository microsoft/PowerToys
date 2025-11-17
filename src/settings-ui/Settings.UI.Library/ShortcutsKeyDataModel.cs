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
        // Suppressing these warnings because removing the setter breaks
        // deserialization with System.Text.Json. This affects the UI display.
        // See: https://github.com/dotnet/runtime/issues/30258
        [JsonPropertyName("global")]
        public List<KeysDataModel> GlobalRemapShortcuts { get; set; }

        [JsonPropertyName("appSpecific")]
        public List<AppSpecificKeysDataModel> AppSpecificRemapShortcuts { get; set; }

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
