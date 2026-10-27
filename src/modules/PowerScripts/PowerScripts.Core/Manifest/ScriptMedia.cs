// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Buffers.Binary;

namespace PowerScripts.Core.Manifest;

/// <summary>Bounded inline media, independently of its clipboard, capture, or file source.</summary>
public static class ScriptMedia
{
    public const int MaxItemBytes = 16 * 1024 * 1024;
    public const int MaxTotalBytes = 32 * 1024 * 1024;
    public const int MaxItems = 64;
    public const int MaxEncodedLength = ((MaxItemBytes + 2) / 3) * 4;
    public const int MaxRequestCharacters = ((MaxTotalBytes + 2) / 3) * 4 + 1024 * 1024;

    public static IReadOnlyList<string> ImageMimeTypes { get; } = Array.AsReadOnly(new[] { "image/png", "image/jpeg", "image/webp" });

    public static IReadOnlyList<string> AudioMimeTypes { get; } = Array.AsReadOnly(new[] { "audio/wav", "audio/mpeg", "audio/flac" });

    public static void ValidateDefinition(JsonSchemaProperty schema, string category)
    {
        var item = schema.Type == "array" ? schema.Items! : schema;
        var allowed = category == "image" ? ImageMimeTypes : AudioMimeTypes;
        if (schema.Type is not ("object" or "array") || item.Type != "object" ||
            item.AdditionalProperties != false ||
            item.Properties is not { Count: 2 } properties ||
            !properties.TryGetValue("mimeType", out var mime) || mime.Type != "string" ||
            mime.Enum is not { Count: > 0 } choices || choices.Any(value => !allowed.Contains(value, StringComparer.Ordinal)) ||
            !properties.TryGetValue("data", out var data) || data.Type != "string" ||
            data.ContentEncoding != "base64" ||
            item.Required is not { Count: 2 } required || !required.Contains("mimeType") || !required.Contains("data") ||
            (data.ContentMediaType is { } fixedMime && (choices.Count != 1 || choices[0] != fixedMime)) ||
            (schema.Type == "array" && (schema.MinItems is null or < 1 || schema.MinItems > MaxItems || schema.MaxItems > MaxItems)))
        {
            throw new InvalidDataException($"Primary '{category}' must be a media object, or a nonempty array of media objects, requiring 'mimeType' (a supported MIME enum) and 'data' (a base64 string). Collections permit at most {MaxItems} items.");
        }
    }

    public static void Validate(JsonElement value, string category, string path)
    {
        var total = 0;
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() is < 1 or > MaxItems)
            {
                throw new InvalidDataException($"'{path}' must contain between 1 and {MaxItems} media items.");
            }

            foreach (var item in value.EnumerateArray())
            {
                total += ValidateItem(item, category, path + "[]");
                if (total > MaxTotalBytes)
                {
                    throw new InvalidDataException($"'{path}' exceeds the 32 MiB decoded media limit.");
                }
            }
        }
        else
        {
            _ = ValidateItem(value, category, path);
        }
    }

    public static byte[] Decode(string data, string path)
    {
        if (data.Length is 0 or > MaxEncodedLength ||
            data.Length % 4 != 0 || data.Any(char.IsWhiteSpace))
        {
            throw new InvalidDataException($"'{path}' must be nonempty base64 without whitespace, at most 16 MiB decoded.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"'{path}' is not valid base64.", ex);
        }

        if (bytes.Length is 0 or > MaxItemBytes)
        {
            throw new InvalidDataException($"'{path}' exceeds the 16 MiB decoded media limit.");
        }

        return bytes;
    }

    private static int ValidateItem(JsonElement item, string category, string path)
    {
        var mime = item.GetProperty("mimeType").GetString()!;
        var allowed = category == "image" ? ImageMimeTypes : AudioMimeTypes;
        if (!allowed.Contains(mime, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"'{path}.mimeType' is not a supported {category} format.");
        }

        var bytes = Decode(item.GetProperty("data").GetString()!, path + ".data");
        ValidateFormat(bytes, mime, path);
        return bytes.Length;
    }

    public static void ValidateFormat(ReadOnlySpan<byte> bytes, string mime, string path)
    {
        var valid = mime switch
        {
            "image/png" => IsStaticPng(bytes),
            "image/jpeg" => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/webp" => IsStaticWebP(bytes),
            "audio/wav" => bytes.Length >= 44 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8),
            "audio/mpeg" => IsMp3(bytes),
            "audio/flac" => bytes.Length >= 42 && bytes[..4].SequenceEqual("fLaC"u8) &&
                (bytes[4] & 0x7f) == 0 && bytes[5] == 0 && bytes[6] == 0 && bytes[7] == 34,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidDataException($"'{path}' has an invalid or unsupported {mime} header. Animated images are not supported. Media is not automatically converted.");
        }
    }

    private static bool IsStaticWebP(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WEBP"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) != bytes.Length - 8)
        {
            return false;
        }

        var image = false;
        var offset = 12;
        while (offset <= bytes.Length - 8)
        {
            var chunk = bytes.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            if (size > bytes.Length - offset - 8 || chunk.SequenceEqual("ANIM"u8) || chunk.SequenceEqual("ANMF"u8) ||
                (chunk.SequenceEqual("VP8X"u8) && (size < 10 || (bytes[offset + 8] & 2) != 0)))
            {
                return false;
            }

            image |= (chunk.SequenceEqual("VP8 "u8) && size >= 10) || (chunk.SequenceEqual("VP8L"u8) && size >= 5);
            offset += 8 + (int)size + (int)(size & 1);
        }

        return image && offset == bytes.Length;
    }

    private static bool IsStaticPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }

        var offset = 8;
        var image = false;
        while (offset <= bytes.Length - 12)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            var chunk = bytes.Slice(offset + 4, 4);
            if (size > bytes.Length - offset - 12 || chunk.SequenceEqual("acTL"u8))
            {
                return false;
            }

            image |= chunk.SequenceEqual("IDAT"u8);
            offset += 12 + (int)size;
            if (chunk.SequenceEqual("IEND"u8))
            {
                return image && size == 0 && offset == bytes.Length;
            }
        }

        return false;
    }

    private static bool IsMp3(ReadOnlySpan<byte> bytes)
    {
        var offset = 0;
        if (bytes.Length >= 10 && bytes[..3].SequenceEqual("ID3"u8))
        {
            if ((bytes[6] | bytes[7] | bytes[8] | bytes[9]) >= 128)
            {
                return false;
            }

            offset = 10 + (bytes[6] << 21 | bytes[7] << 14 | bytes[8] << 7 | bytes[9]);
            if (bytes[3] == 4 && (bytes[5] & 16) != 0)
            {
                offset += 10;
            }
        }

        return offset <= bytes.Length - 4 && bytes[offset] == 0xff && (bytes[offset + 1] & 0xe0) == 0xe0 &&
            (bytes[offset + 1] & 0x18) != 8 && (bytes[offset + 1] & 6) == 2 &&
            (bytes[offset + 2] & 0xf0) is not (0 or 0xf0) && (bytes[offset + 2] & 0x0c) != 0x0c;
    }
}
