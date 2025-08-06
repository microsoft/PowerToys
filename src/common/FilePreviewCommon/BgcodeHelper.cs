// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <summary>
        /// Gets any thumbnails found in a bgcode file.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> instance to the bgcode file.</param>
        /// <returns>The thumbnails found in a bgcode file.</returns>
        public static IEnumerable<BgcodeThumbnail> GetThumbnails(BinaryReader reader)
        {
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
                var blockType = (BgcodeBlockType)reader.ReadUInt16();
                var compression = (BgcodeCompressionType)reader.ReadUInt16();
                var uncompressedSize = reader.ReadUInt32();

                var size = compression == BgcodeCompressionType.NoCompression ? uncompressedSize : reader.ReadUInt32();

                switch (blockType)
                {
                    case BgcodeBlockType.FileMetadataBlock:
                    case BgcodeBlockType.PrinterMetadataBlock:
                    case BgcodeBlockType.PrintMetadataBlock:
                    case BgcodeBlockType.SlicerMetadataBlock:
                    case BgcodeBlockType.GCodeBlock:
                        reader.BaseStream.Seek(2 + size, SeekOrigin.Current); // Skip

                        break;

                    case BgcodeBlockType.ThumbnailBlock:
                        var format = (BgcodeThumbnailFormat)reader.ReadUInt16();

                        reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip width and height

                        var data = ReadAndDecompressData(reader, compression, (int)size);

                        if (data != null)
                        {
                            yield return new BgcodeThumbnail(format, data);
                        }

                        break;
                }

                if (checksum == BgcodeChecksumType.CRC32)
                {
                    reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip checksum
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

        private static byte[]? ReadAndDecompressData(BinaryReader reader, BgcodeCompressionType compression, int size)
        {
            // Though the spec doesn't actually mention it, the reference encoder code never applies compression to thumbnails data
            // which makes complete sense as this data is PNG, JPEG or QOI encoded so already compressed as much as possible!
            switch (compression)
            {
                case BgcodeCompressionType.NoCompression:
                    return reader.ReadBytes(size);

                case BgcodeCompressionType.DeflateAlgorithm:
                    var buffer = new byte[size];

                    using (var deflateStream = new DeflateStream(reader.BaseStream, CompressionMode.Decompress, true))
                    {
                        deflateStream.ReadExactly(buffer, 0, size);
                    }

                    return buffer;

                default:
                    reader.BaseStream.Seek(size, SeekOrigin.Current); // Skip unknown or unsupported compression types

                    return null;
            }
        }
    }
}
