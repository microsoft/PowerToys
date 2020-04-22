// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ShortcutsKeyDataModel
    {
        [JsonPropertyName("global")]
        public List<KeysDataModel> GlobalRemapShortcuts { get; set; }

        public ShortcutsKeyDataModel()
        {
            GlobalRemapShortcuts = new List<KeysDataModel>();
        }
    }
}
