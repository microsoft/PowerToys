// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeysDataModel
    {
        [JsonPropertyName("originalKeys")]
        public string OriginalKeys { get; set; }

        [JsonPropertyName("newRemapKeys")]
        public string NewRemapKeys { get; set; }

        private List<string> MapKeys(string stringOfKeys)
        {
            return stringOfKeys
                .Split(';')
                .Select(uint.Parse)
                .Select(Helper.GetKeyName)
                .ToList();
        }

        public List<string> GetOriginalKeys()
        {
            return MapKeys(OriginalKeys);
        }

        public List<string> GetNewRemapKeys()
        {
            return MapKeys(NewRemapKeys);
        }
    }
}
