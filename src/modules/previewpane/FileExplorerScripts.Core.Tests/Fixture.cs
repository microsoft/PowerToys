// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text.Json;

namespace FileExplorerScripts.Core.Tests;

internal sealed class Fixture : IDisposable
{
    internal Fixture()
    {
        Root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N")[..8])).FullName;
        Destination = Directory.CreateDirectory(Path.Combine(Root, "out")).FullName;
        Input = Write("sample.md", "original input");
    }

    internal string Root { get; }

    internal string Destination { get; }

    internal string Input { get; }

    internal string Write(string name, string contents)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    internal string Write(string name, byte[] contents)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    internal static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7N8AAAAASUVORK5CYII=");

    internal static byte[] Wave()
    {
        var bytes = new byte[46];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 38);
        "WAVEfmt "u8.CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 44100);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 88200);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 16);
        "data"u8.CopyTo(bytes.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), 2);
        return bytes;
    }

    internal static JsonElement MediaSchema(string kind, string mime, bool array = false)
    {
        var primary = $$$"""
            {"type":"object","properties":{"mimeType":{"type":"string","enum":["{{{mime}}}"]},"data":{"type":"string","contentEncoding":"base64"}},"required":["mimeType","data"],"additionalProperties":false}
            """;
        if (array)
        {
            primary = $$$"""{"type":"array","minItems":1,"maxItems":2,"items":{{{primary}}}}""";
        }

        return JsonSerializer.Deserialize<JsonElement>($$$"""{"type":"object","properties":{"{{{kind}}}":{{{primary}}}},"required":["{{{kind}}}"],"additionalProperties":false}""");
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
