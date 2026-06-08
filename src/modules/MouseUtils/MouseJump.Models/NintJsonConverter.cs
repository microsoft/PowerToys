// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseJump.Models;

public sealed class NintJsonConverter : JsonConverter<nint>
{
    public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // nint can be 32 or 64 bit, so convert to Int64
        var value = reader.GetInt64();
        return (nint)value;
    }

    public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((long)value);
    }
}
