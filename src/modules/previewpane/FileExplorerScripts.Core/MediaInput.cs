// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

public static class MediaInput
{
    private const FileAttributes UnavailableAttributes = FileAttributes.Offline | (FileAttributes)0x440000;

    public sealed record Contract(bool IsArray, int Minimum, int Maximum, IReadOnlyList<string> MimeTypes);

    public static Contract GetContract(PowerScriptInfo script)
    {
        var field = script.Input == PowerScriptIO.Image ? "image" : "audio";
        if (script.InputSchema is not { ValueKind: JsonValueKind.Object } schema ||
            !schema.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty(field, out var primary))
        {
            throw new InvalidDataException("The media input schema is missing.");
        }

        var array = primary.GetProperty("type").GetString() == "array";
        var item = array ? primary.GetProperty("items") : primary;
        return new Contract(
            array,
            array && primary.TryGetProperty("minItems", out var min) ? min.GetInt32() : 1,
            array && primary.TryGetProperty("maxItems", out var max) ? Math.Min(max.GetInt32(), 64) : array ? 64 : 1,
            item.GetProperty("properties").GetProperty("mimeType").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    public static bool Accepts(PowerScriptInfo script, string? mimeType)
    {
        if (mimeType is null || !mimeType.StartsWith(script.Input == PowerScriptIO.Image ? "image/" : "audio/", StringComparison.Ordinal))
        {
            return false;
        }

        var contract = GetContract(script);
        return contract.Minimum <= 1 && contract.Maximum >= 1 && contract.MimeTypes.Contains(mimeType, StringComparer.Ordinal);
    }

    internal static string? Probe(string path)
    {
        if (MimeType(path) is not { } mime)
        {
            return null;
        }

        try
        {
            // Menu matching must not hydrate cloud placeholders or load an entire media file.
            if ((File.GetAttributes(path) & UnavailableAttributes) != 0)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1);
            if (stream.Length is <= 0 or > PowerScriptMedia.MaxItemBytes)
            {
                return null;
            }

            Span<byte> header = stackalloc byte[64];
            var count = (int)Math.Min(header.Length, stream.Length);
            stream.ReadExactly(header[..count]);
            if (mime == "audio/mpeg" && count >= 3 && header[..3].SequenceEqual("ID3"u8))
            {
                var offset = MediaFormats.Mp3FrameOffset(header[..count], stream.Length);
                if (offset < 0)
                {
                    return null;
                }

                stream.Position = offset;
                stream.ReadExactly(header[..4]);
                count = 4;
            }

            return MediaFormats.HasHeader(mime, header[..count], stream.Length) ? mime : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static async Task<PowerScriptMedia> ReadAsync(PowerScriptInfo script, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LocalPaths.Validate(path, directory: false);
        if ((File.GetAttributes(path) & UnavailableAttributes) != 0)
        {
            throw new InvalidDataException("The selected media must be available locally.");
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (stream.Length is <= 0 or > PowerScriptMedia.MaxItemBytes)
        {
            throw new InvalidDataException("Inline media must contain at most 16 MiB. Use a files-input script for larger files.");
        }

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        var mime = MimeType(path);
        if (stream.ReadByte() != -1 || mime is null || !MediaFormats.HasHeader(mime, bytes, bytes.Length) || !Accepts(script, mime))
        {
            throw new InvalidDataException("The selected file no longer matches the script's image/audio input.");
        }

        var item = new PowerScriptMediaItem(mime, bytes);
        return GetContract(script).IsArray ? new PowerScriptMedia(new[] { item }) : new PowerScriptMedia(item);
    }

    internal static string? MimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".flac" => "audio/flac",
        _ => null,
    };
}
