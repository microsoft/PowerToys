// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

internal static class MediaFormats
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    internal static string GetExtension(string mimeType, ReadOnlySpan<byte> data, PowerScriptIO output)
    {
        if (output == PowerScriptIO.Image && mimeType == "image/bmp" && IsBitmap(data))
        {
            return ".bmp";
        }

        if (output == PowerScriptIO.Audio && mimeType == "audio/wav" && IsWave(data))
        {
            return ".wav";
        }

        if (mimeType.StartsWith(output == PowerScriptIO.Image ? "image/" : "audio/", StringComparison.Ordinal) &&
            HasHeader(mimeType, data, data.Length))
        {
            var extension = mimeType switch
            {
                "image/png" when IsPng(data) => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" when IsWebP(data) => ".webp",
                "audio/mpeg" => ".mp3",
                "audio/flac" => ".flac",
                _ => null,
            };
            if (extension is not null)
            {
                return extension;
            }
        }

        throw new InvalidDataException("Unsupported or malformed inline media. Return unsupported formats as files.");
    }

    internal static bool HasHeader(string mime, ReadOnlySpan<byte> data, long length) => mime switch
    {
        "image/png" => length >= 33 && data.Length >= 16 && data[..8].SequenceEqual(PngSignature) && data.Slice(12, 4).SequenceEqual("IHDR"u8),
        "image/jpeg" => length >= 4 && data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff,
        "image/webp" => length >= 20 && data.Length >= 20 && data[..4].SequenceEqual("RIFF"u8) &&
            data.Slice(8, 4).SequenceEqual("WEBP"u8) && U32(data, 4) == length - 8,
        "audio/wav" => length >= 44 && data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) &&
            data.Slice(8, 4).SequenceEqual("WAVE"u8) && U32(data, 4) == length - 8,
        "audio/flac" => length >= 42 && data.Length >= 8 && data[..4].SequenceEqual("fLaC"u8) &&
            (data[4] & 0x7f) == 0 && data[5] == 0 && data[6] == 0 && data[7] == 34,
        "audio/mpeg" => IsMp3(data, length),
        _ => false,
    };

    private static bool IsMp3(ReadOnlySpan<byte> data, long length)
    {
        var offset = Mp3FrameOffset(data, length);
        return offset >= 0 && offset <= data.Length - 4 && data[offset] == 0xff && (data[offset + 1] & 0xe0) == 0xe0 &&
            (data[offset + 1] & 0x18) != 8 && (data[offset + 1] & 6) == 2 &&
            (data[offset + 2] & 0xf0) is not (0 or 0xf0) && (data[offset + 2] & 0x0c) != 0x0c;
    }

    internal static int Mp3FrameOffset(ReadOnlySpan<byte> data, long length)
    {
        if (data.Length >= 3 && data[..3].SequenceEqual("ID3"u8))
        {
            if (data.Length < 10 || data[3] is not (2 or 3 or 4) || (data[6] | data[7] | data[8] | data[9]) >= 128)
            {
                return -1;
            }

            var offset = 10 + (data[6] << 21 | data[7] << 14 | data[8] << 7 | data[9]);
            if (data[3] == 4 && (data[5] & 16) != 0)
            {
                offset += 10;
            }

            return offset <= length - 4 ? offset : -1;
        }

        return 0;
    }

    private static bool IsPng(ReadOnlySpan<byte> data)
    {
        var image = false;
        var offset = 8;
        while (offset <= data.Length - 12)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            var chunk = data.Slice(offset + 4, 4);
            if (size > data.Length - offset - 12 || chunk.SequenceEqual("acTL"u8))
            {
                return false;
            }

            image |= chunk.SequenceEqual("IDAT"u8);
            offset += 12 + (int)size;
            if (chunk.SequenceEqual("IEND"u8))
            {
                return image && size == 0 && offset == data.Length;
            }
        }

        return false;
    }

    private static bool IsWebP(ReadOnlySpan<byte> data)
    {
        var image = false;
        var offset = 12;
        while (offset <= data.Length - 8)
        {
            var chunk = data.Slice(offset, 4);
            var size = U32(data, offset + 4);
            if (size > data.Length - offset - 8 || chunk.SequenceEqual("ANIM"u8) || chunk.SequenceEqual("ANMF"u8) ||
                (chunk.SequenceEqual("VP8X"u8) && (size < 10 || (data[offset + 8] & 2) != 0)))
            {
                return false;
            }

            image |= (chunk.SequenceEqual("VP8 "u8) && size >= 10) || (chunk.SequenceEqual("VP8L"u8) && size >= 5);
            offset += 8 + (int)size + (int)(size & 1);
        }

        return image && offset == data.Length;
    }

    private static bool IsBitmap(ReadOnlySpan<byte> data)
    {
        if (data.Length < 54 || !data[..2].SequenceEqual("BM"u8) ||
            U32(data, 2) != data.Length || U32(data, 6) != 0 ||
            U32(data, 10) != 54 || U32(data, 14) != 40 ||
            U16(data, 26) != 1 || U32(data, 30) != 0 ||
            U32(data, 46) != 0 || U32(data, 50) != 0)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(data[18..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(data[22..]);
        var bits = U16(data, 28);
        if (width <= 0 || height is 0 or int.MinValue || bits is not (24 or 32))
        {
            return false;
        }

        var stride = ((((long)width * bits) + 31) / 32) * 4;
        if (stride > data.Length)
        {
            return false;
        }

        var size = stride * Math.Abs((long)height);
        return size == data.Length - 54 && (U32(data, 34) == 0 || U32(data, 34) == size);
    }

    private static bool IsWave(ReadOnlySpan<byte> data)
    {
        if (data.Length < 44 || !data[..4].SequenceEqual("RIFF"u8) ||
            U32(data, 4) != data.Length - 8 || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return false;
        }

        var formatSeen = false;
        var dataSeen = false;
        var block = 0;
        var offset = 12;
        while (offset < data.Length)
        {
            if (data.Length - offset < 8)
            {
                return false;
            }

            var size = U32(data, offset + 4);
            var end = (long)offset + 8 + size;
            var paddedEnd = end + (size & 1);
            if (paddedEnd > data.Length)
            {
                return false;
            }

            var tag = data.Slice(offset, 4);
            if (tag.SequenceEqual("fmt "u8))
            {
                if (formatSeen || dataSeen || size != 16 || U16(data, offset + 8) != 1)
                {
                    return false;
                }

                var channels = U16(data, offset + 10);
                var rate = U32(data, offset + 12);
                var byteRate = U32(data, offset + 16);
                block = U16(data, offset + 20);
                var bits = U16(data, offset + 22);
                if (channels is < 1 or > 32 || rate is < 1 or > 768000 ||
                    bits is not (8 or 16 or 24 or 32) || block != channels * (bits / 8) ||
                    byteRate != (long)rate * block)
                {
                    return false;
                }

                formatSeen = true;
            }
            else if (tag.SequenceEqual("data"u8))
            {
                if (!formatSeen || dataSeen || size == 0 || size % block != 0)
                {
                    return false;
                }

                dataSeen = true;
            }
            else
            {
                return false;
            }

            offset = (int)paddedEnd;
        }

        return formatSeen && dataSeen;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    private static uint U32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
}
