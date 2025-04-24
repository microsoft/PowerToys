// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.JsonSerilizerContext;

namespace Microsoft.CmdPal.Common.Helpers;

public static partial class Json
{
    public static async Task<T> ToObjectAsync<T>(string value)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        JsonTypeInfo<T>? typeInfo = (JsonTypeInfo<T>?)CommonSerializationContext.Default.GetTypeInfo(typeof(T));
        if (typeInfo == null)
        {
            throw new InvalidOperationException($"Type {typeof(T)} is not supported for deSerialization.");
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return (await JsonSerializer.DeserializeAsync<T>(stream, typeInfo))!;
    }

    public static async Task<string> StringifyAsync<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return value!.ToString()!.ToLowerInvariant();
        }

        JsonTypeInfo<T>? typeInfo = (JsonTypeInfo<T>?)CommonSerializationContext.Default.GetTypeInfo(typeof(T));
        if (typeInfo == null)
        {
            throw new InvalidOperationException($"Type {typeof(T)} is not supported for serialization.");
        }

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, typeInfo);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
