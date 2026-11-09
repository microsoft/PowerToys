// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>Bounded, length-prefixed UTF-8 JSON transport for consumer-owned Host stdio sessions.</summary>
public static class PowerScriptsSessionProtocol
{
    /// <summary>Maximum frame size, including the escaped inline-media request or response.</summary>
    public const int MaxFrameBytes = 128 * 1024 * 1024;

    /// <summary>Reads one frame, or null for EOF between requests. Truncated frames are errors.</summary>
    public static async Task<byte[]?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var header = new byte[4];
        if (await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0)
        {
            return null;
        }

        await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxFrameBytes)
        {
            throw new InvalidDataException("Invalid PowerScripts session frame size.");
        }

        var frame = new byte[length];
        await stream.ReadExactlyAsync(frame, cancellationToken).ConfigureAwait(false);
        return frame;
    }

    /// <summary>Writes and flushes one bounded frame.</summary>
    public static async Task WriteAsync(Stream stream, byte[] frame, CancellationToken cancellationToken = default)
    {
        if (frame.Length == 0 || frame.Length > MaxFrameBytes)
        {
            throw new InvalidDataException("PowerScripts session frame exceeds its size limit.");
        }

        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, frame.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serializes a command and its optional stdin payload without reflection.</summary>
    public static byte[] CreateRequest(IEnumerable<string> arguments, string? input)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("arguments");
            foreach (var argument in arguments)
            {
                writer.WriteStringValue(argument);
            }

            writer.WriteEndArray();
            writer.WriteString("input", input);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>Serializes the existing CLI exit status and output streams.</summary>
    public static byte[] CreateResponse(int exitCode, string stdout, string stderr)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("exitCode", exitCode);
            writer.WriteString("stdout", stdout);
            writer.WriteString("stderr", stderr);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
