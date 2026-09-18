// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.PowerToys.FilePreviewCommon;
using Microsoft.PowerToys.ThumbnailHandler.Bgcode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BgcodeThumbnailProviderUnitTests
{
    [STATestClass]
    public class BgcodeThumbnailProviderTests
    {
        [DataTestMethod]
        [DataRow("HelperFiles/sample.bgcode")]
        public void GetThumbnailValidStreamBgcode(string filePath)
        {
            // Act
            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeBgcode()
        {
            // Act
            var filePath = "HelperFiles/sample.bgcode";

            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigBgcode()
        {
            // Act
            var filePath = "HelperFiles/sample.bgcode";

            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoBgcodeEmptyDataShouldReturnNullBitmap()
        {
            using (var reader = new BinaryReader(new MemoryStream()))
            {
                Bitmap thumbnail = BgcodeThumbnailProvider.GetThumbnail(reader, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoBgcodeNullStringShouldReturnNullBitmap()
        {
            Bitmap thumbnail = BgcodeThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void WrappedBlockSizeShouldBeRejected()
        {
            using var reader = CreateReader(writer =>
            {
                WriteBlockHeader(writer, BgcodeBlockType.ThumbnailBlock, BgcodeCompressionType.NoCompression, uint.MaxValue);
                writer.Write((ushort)BgcodeThumbnailFormat.PNG);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
            });

            Assert.ThrowsExactly<InvalidDataException>(() => BgcodeHelper.GetBestThumbnail(reader));
        }

        [TestMethod]
        public void TruncatedThumbnailBlockShouldBeRejected()
        {
            using var reader = CreateReader(writer =>
            {
                WriteBlockHeader(writer, BgcodeBlockType.ThumbnailBlock, BgcodeCompressionType.NoCompression, 8);
                writer.Write((ushort)BgcodeThumbnailFormat.PNG);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write(new byte[] { 1, 2 });
            });

            Assert.ThrowsExactly<InvalidDataException>(() => BgcodeHelper.GetBestThumbnail(reader));
        }

        [TestMethod]
        public void UnsupportedCompressionShouldBeSkippedAndNextBlockParsed()
        {
            var expected = new byte[] { 1, 2, 3, 4 };
            using var reader = CreateReader(writer =>
            {
                WriteBlockHeader(
                    writer,
                    BgcodeBlockType.ThumbnailBlock,
                    BgcodeCompressionType.HeatshrinkAlgorithm11,
                    uncompressedSize: 100,
                    storedSize: 3);
                WriteThumbnailMetadata(writer);
                writer.Write(new byte[] { 9, 8, 7 });

                WriteBlockHeader(
                    writer,
                    BgcodeBlockType.ThumbnailBlock,
                    BgcodeCompressionType.NoCompression,
                    (uint)expected.Length);
                WriteThumbnailMetadata(writer);
                writer.Write(expected);
            });

            var thumbnails = BgcodeHelper.GetThumbnails(reader).ToList();

            Assert.HasCount(1, thumbnails);
            CollectionAssert.AreEqual(expected, thumbnails[0].Data);
        }

        [TestMethod]
        public void DeflateBlockShouldConsumeOnlyItsStoredPayloadAndMakeForwardProgress()
        {
            var expected = new byte[] { 1, 2, 3, 4, 5 };
            byte[] compressed;
            using (var compressedStream = new MemoryStream())
            {
                using (var deflate = new DeflateStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
                {
                    deflate.Write(expected);
                }

                compressed = compressedStream.ToArray();
            }

            using var reader = CreateReader(writer =>
            {
                WriteBlockHeader(
                    writer,
                    BgcodeBlockType.ThumbnailBlock,
                    BgcodeCompressionType.DeflateAlgorithm,
                    (uint)expected.Length,
                    (uint)compressed.Length);
                WriteThumbnailMetadata(writer);
                writer.Write(compressed);

                WriteBlockHeader(writer, BgcodeBlockType.FileMetadataBlock, BgcodeCompressionType.NoCompression, 0);
                writer.Write((ushort)0);
            });

            var thumbnails = BgcodeHelper.GetThumbnails(reader).ToList();

            Assert.HasCount(1, thumbnails);
            CollectionAssert.AreEqual(expected, thumbnails[0].Data);
            Assert.AreEqual(reader.BaseStream.Length, reader.BaseStream.Position);
        }

        [TestMethod]
        public void ValidCrc32FileWithMetadataAndThumbnailShouldParse()
        {
            var expected = new byte[] { 1, 2, 3, 4 };
            using var reader = CreateReader(
                writer =>
                {
                    WriteBlockHeader(writer, BgcodeBlockType.FileMetadataBlock, BgcodeCompressionType.NoCompression, 3);
                    writer.Write((ushort)0);
                    writer.Write(new byte[] { 7, 8, 9 });
                    writer.Write(0x12345678U);

                    WriteBlockHeader(
                        writer,
                        BgcodeBlockType.ThumbnailBlock,
                        BgcodeCompressionType.NoCompression,
                        (uint)expected.Length);
                    WriteThumbnailMetadata(writer);
                    writer.Write(expected);
                    writer.Write(0x87654321U);
                },
                BgcodeChecksumType.CRC32);

            var thumbnails = BgcodeHelper.GetThumbnails(reader).ToList();

            Assert.HasCount(1, thumbnails);
            CollectionAssert.AreEqual(expected, thumbnails[0].Data);
            Assert.AreEqual(reader.BaseStream.Length, reader.BaseStream.Position);
        }

        [TestMethod]
        public void WrappedMetadataBlockSizeShouldBeRejectedBeforeRepeatedReads()
        {
            using var stream = new SparseWrappedMetadataStream();
            using var reader = new BinaryReader(stream);

            Assert.ThrowsExactly<InvalidDataException>(() => BgcodeHelper.GetBestThumbnail(reader));
            Assert.IsTrue(stream.ReadCallCount <= 8, $"Parser performed {stream.ReadCallCount} reads.");
        }

        [TestMethod]
        public void DeflateThumbnailAboveAllocationLimitShouldBeRejectedBeforeAllocation()
        {
            using var reader = CreateReader(writer =>
            {
                WriteBlockHeader(
                    writer,
                    BgcodeBlockType.ThumbnailBlock,
                    BgcodeCompressionType.DeflateAlgorithm,
                    uncompressedSize: (64U * 1024 * 1024) + 1,
                    storedSize: 0);
                WriteThumbnailMetadata(writer);
            });

            var exception = Assert.ThrowsExactly<InvalidDataException>(() => BgcodeHelper.GetBestThumbnail(reader));

            Assert.AreEqual("The BGCODE thumbnail block exceeds the 64 MiB limit.", exception.Message);
        }

        private static BinaryReader CreateReader(
            Action<BinaryWriter> writeBlocks,
            BgcodeChecksumType checksum = BgcodeChecksumType.None)
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((byte)'G');
                writer.Write((byte)'C');
                writer.Write((byte)'D');
                writer.Write((byte)'E');
                writer.Write(1U);
                writer.Write((ushort)checksum);
                writeBlocks(writer);
            }

            stream.Position = 0;
            return new BinaryReader(stream);
        }

        private static void WriteBlockHeader(
            BinaryWriter writer,
            BgcodeBlockType blockType,
            BgcodeCompressionType compression,
            uint uncompressedSize,
            uint storedSize = 0)
        {
            writer.Write((ushort)blockType);
            writer.Write((ushort)compression);
            writer.Write(uncompressedSize);
            if (compression != BgcodeCompressionType.NoCompression)
            {
                writer.Write(storedSize);
            }
        }

        private static void WriteThumbnailMetadata(BinaryWriter writer)
        {
            writer.Write((ushort)BgcodeThumbnailFormat.PNG);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
        }

        private sealed class SparseWrappedMetadataStream : Stream
        {
            private const int MaximumReadCalls = 16;
            private readonly byte[] prefix;

            public SparseWrappedMetadataStream()
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((byte)'G');
                    writer.Write((byte)'C');
                    writer.Write((byte)'D');
                    writer.Write((byte)'E');
                    writer.Write(1U);
                    writer.Write((ushort)BgcodeChecksumType.None);
                    writer.Write((ushort)BgcodeBlockType.GCodeBlock);
                    writer.Write((ushort)BgcodeCompressionType.NoCompression);
                    writer.Write(0xFFFFFFFEU);
                }

                prefix = stream.ToArray();
            }

            public int ReadCallCount { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => 1L << 31;

            public override long Position { get; set; }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (++ReadCallCount > MaximumReadCalls)
                {
                    throw new InvalidOperationException("The parser exceeded the bounded read count.");
                }

                var available = (int)Math.Min(count, Length - Position);
                for (var index = 0; index < available; index++)
                {
                    var sourcePosition = Position + index;
                    buffer[offset + index] = sourcePosition < prefix.Length ? prefix[(int)sourcePosition] : (byte)0;
                }

                Position += available;
                return available;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Position + offset,
                    SeekOrigin.End => Length + offset,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };

                return Position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
