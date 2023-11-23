// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeysDataModel
    {
        [JsonPropertyName("originalKeys")]
        public string OriginalKeys { get; set; }

        [JsonPropertyName("newRemapKeys")]
        public string NewRemapKeys { get; set; }

        [JsonPropertyName("unicodeText")]
        public string NewRemapString { get; set; }

        private static List<string> MapKeys(string stringOfKeys)
        {
            return stringOfKeys
                .Split(';')
                .Select(uint.Parse)
                .Select(Helper.GetKeyName)
                .ToList();
        }

        public List<string> GetMappedOriginalKeys()
        {
            return MapKeys(OriginalKeys);
        }

        public List<string> GetMappedNewRemapKeys()
        {
            return string.IsNullOrEmpty(NewRemapString) ? MapKeys(NewRemapKeys) : new List<string> { NewRemapString };
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
