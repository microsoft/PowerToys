// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    // Deserialization of interfaces is not allowed by JsonConverter as we do not have enough information about the class type to deserialize to.
    // This custom Json Converter helps deserialize interfaces to the given class.
    public class InterfaceConverter<TClass, TInterface> : JsonConverter<TInterface>
        where TClass : class, TInterface
    {
        // Custom serializer specifying the class name to deserialize to.
        public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TClass>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (TClass)value);
        }
    }
}
