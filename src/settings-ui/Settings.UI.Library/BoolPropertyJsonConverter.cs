// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class BoolPropertyJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var boolProperty = JsonSerializer.Deserialize<BoolProperty>(ref reader, options);
            return boolProperty.Value;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            var boolProperty = new BoolProperty(value);
            JsonSerializer.Serialize(writer, boolProperty, options);
        }
    }
}
