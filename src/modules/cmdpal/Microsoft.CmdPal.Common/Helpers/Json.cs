// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Helpers;

public static class Json
{
    public static async Task<T> ToObjectAsync<T>(string value)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return (await JsonSerializer.DeserializeAsync<T>(stream))!;
    }

    public static async Task<string> StringifyAsync<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return value!.ToString()!.ToLowerInvariant();
        }

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
