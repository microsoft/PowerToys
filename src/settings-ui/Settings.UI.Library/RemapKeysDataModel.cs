// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class RemapKeysDataModel
    {
        // Suppressing this warning because removing the setter breaks
        // deserialization with System.Text.Json. This affects the UI display.
        // See: https://github.com/dotnet/runtime/issues/30258
        [JsonPropertyName("inProcess")]
        public List<KeysDataModel> InProcessRemapKeys { get; set; }

        public RemapKeysDataModel()
        {
            InProcessRemapKeys = new List<KeysDataModel>();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
