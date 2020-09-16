// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class InterfaceConverter<TM, TI> : JsonConverter<TI>
        where TM : class, TI
    {
        public override TI Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TM>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TI value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (TM)value);
        }
    }
}
