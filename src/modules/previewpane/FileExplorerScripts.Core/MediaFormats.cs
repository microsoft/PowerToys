// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

internal static class MediaFormats
{
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

        throw new InvalidDataException("Unsupported or malformed inline media. Explorer supports only uncompressed 24/32-bit BMP and PCM WAV; return other formats as files.");
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
