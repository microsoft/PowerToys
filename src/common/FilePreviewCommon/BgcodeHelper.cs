// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    /// <summary>
    /// Bgcode file helper class.
    /// </summary>
    public static class BgcodeHelper
    {
        private const uint MagicNumber = 'G' | 'C' << 8 | 'D' << 16 | 'E' << 24;
        private const uint MaximumThumbnailDataSize = 64 * 1024 * 1024;

        /// <summary>
        /// Gets any thumbnails found in a bgcode file.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> instance to the bgcode file.</param>
        /// <returns>The thumbnails found in a bgcode file.</returns>
        public static IEnumerable<BgcodeThumbnail> GetThumbnails(BinaryReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            EnsureRemaining(reader, 10);
            var magicNumber = reader.ReadUInt32();

            if (magicNumber != MagicNumber)
            {
                throw new InvalidDataException("Invalid magic number.");
            }

            var version = reader.ReadUInt32();

            if (version != 1)
            {
                // Version 1 is the only one that exists
                throw new InvalidDataException("Unsupported version.");
            }

            var checksum = (BgcodeChecksumType)reader.ReadUInt16();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var blockStart = reader.BaseStream.Position;
                EnsureRemaining(reader, 8);
                var blockType = (BgcodeBlockType)reader.ReadUInt16();
                var compression = (BgcodeCompressionType)reader.ReadUInt16();
                var uncompressedSize = reader.ReadUInt32();

                if (compression != BgcodeCompressionType.NoCompression)
                {
                    EnsureRemaining(reader, 4);
                }

                var storedSize = compression == BgcodeCompressionType.NoCompression ? uncompressedSize : reader.ReadUInt32();

                switch (blockType)
                {
                    case BgcodeBlockType.FileMetadataBlock:
                    case BgcodeBlockType.PrinterMetadataBlock:
                    case BgcodeBlockType.PrintMetadataBlock:
                    case BgcodeBlockType.SlicerMetadataBlock:
                    case BgcodeBlockType.GCodeBlock:
                        SkipBytes(reader, 2); // Encoding
                        SkipBytes(reader, storedSize);
                        break;

                    case BgcodeBlockType.ThumbnailBlock:
                        EnsureRemaining(reader, 2);
                        var format = (BgcodeThumbnailFormat)reader.ReadUInt16();

                        SkipBytes(reader, 4); // Width and height

                        var data = ReadAndDecompressData(reader, compression, storedSize, uncompressedSize);

                        if (data != null)
                        {
                            yield return new BgcodeThumbnail(format, data);
                        }

                        break;

                    default:
                        throw new InvalidDataException("Unsupported block type.");
                }

                if (checksum == BgcodeChecksumType.CRC32)
                {
                    SkipBytes(reader, 4);
                }

                if (reader.BaseStream.Position <= blockStart)
                {
                    throw new InvalidDataException("The BGCODE parser made no forward progress.");
                }
            }
        }

        /// <summary>
        /// Gets the best thumbnail available in a bgcode file.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> instance to the gcode file.</param>
        /// <returns>The best thumbnail available in the gcode file.</returns>
        public static BgcodeThumbnail? GetBestThumbnail(BinaryReader reader)
        {
            return GetThumbnails(reader)
                .OrderByDescending(x => x.Format switch
                {
                    BgcodeThumbnailFormat.PNG => 2,
                    BgcodeThumbnailFormat.QOI => 1,
                    BgcodeThumbnailFormat.JPG => 0,
                    _ => 0,
                })
                .ThenByDescending(x => x.Data.Length)
                .FirstOrDefault();
        }

        private static byte[]? ReadAndDecompressData(
            BinaryReader reader,
            BgcodeCompressionType compression,
            uint storedSize,
            uint uncompressedSize)
        {
            // Though the spec doesn't actually mention it, the reference encoder code never applies compression to thumbnails data
            // which makes complete sense as this data is PNG, JPEG or QOI encoded so already compressed as much as possible!
            switch (compression)
            {
                case BgcodeCompressionType.NoCompression:
                    EnsureThumbnailDataSize(storedSize);
                    EnsureRemaining(reader, storedSize);
                    return ReadBytesExactly(reader, (int)storedSize);

                case BgcodeCompressionType.DeflateAlgorithm:
                    EnsureThumbnailDataSize(storedSize);
                    EnsureThumbnailDataSize(uncompressedSize);
                    EnsureRemaining(reader, storedSize);
                    var compressedData = ReadBytesExactly(reader, (int)storedSize);
                    var buffer = new byte[(int)uncompressedSize];

                    using (var compressedStream = new MemoryStream(compressedData, false))
                    using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    {
                        try
                        {
                            deflateStream.ReadExactly(buffer);
                        }
                        catch (EndOfStreamException ex)
                        {
                            throw new InvalidDataException("The BGCODE compressed block is truncated.", ex);
                        }
                    }

                    return buffer;

                default:
                    SkipBytes(reader, storedSize);

                    return null;
            }
        }

        private static void EnsureThumbnailDataSize(uint size)
        {
            // BGCODE thumbnails are already encoded PNG, JPEG, or QOI images. A 64 MiB encoded-image
            // ceiling is far above normal slicer previews while bounding every parser-owned byte array.
            if (size > MaximumThumbnailDataSize)
            {
                throw new InvalidDataException("The BGCODE thumbnail block exceeds the 64 MiB limit.");
            }
        }

        private static byte[] ReadBytesExactly(BinaryReader reader, int count)
        {
            var data = reader.ReadBytes(count);
            if (data.Length != count)
            {
                throw new InvalidDataException("The BGCODE block is truncated.");
            }

            return data;
        }

        private static void SkipBytes(BinaryReader reader, uint count)
        {
            EnsureRemaining(reader, count);
            reader.BaseStream.Seek(count, SeekOrigin.Current);
        }

        private static void EnsureRemaining(BinaryReader reader, uint count)
        {
            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining < 0 || (ulong)remaining < count)
            {
                throw new InvalidDataException("The BGCODE block is truncated.");
            }
        }
    }
}
