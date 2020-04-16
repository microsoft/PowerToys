// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyboadManagerConfigModel
    {
        [JsonPropertyName("remapKeys")]
        public List<KeysDataModel> RemapKeys { get; set; }

        [JsonPropertyName("remapShortcuts")]
        public List<KeysDataModel> RemapShortcuts { get; set; }
    }
}
