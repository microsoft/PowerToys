// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeColorProfileTests
    {
        private const int SourceWidth = 64;
        private const int SourceHeight = 32;

        private static readonly byte[] SourceBgra = [192, 128, 64, 255];

        [TestMethod]
        [DataRow("png")]
        [DataRow("tif")]
        public async Task ExecutePreservesDisplayedColorWithExplicitEncodingOption(string extension)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "source." + extension);
            var profile = CreateLinearRgbProfile();
            if (extension == "png")
            {
                WriteProfiledPng(source, profile);
            }
            else
            {
                WriteProfiledTiff(source, profile);
            }

            byte[] expectedDisplayColor = null;
            await AssertEx.ImageAsync(
                source,
                async decoder =>
                {
                    Assert.AreEqual(1u, decoder.FrameCount);
                    var frame = await decoder.GetFrameAsync(0);
                    Assert.AreEqual((uint)SourceWidth, frame.PixelWidth);
                    Assert.AreEqual((uint)SourceHeight, frame.PixelHeight);
                    var raw = await ReadPixelsAsync(frame, ColorManagementMode.DoNotColorManage);
                    AssertUniformColor(raw, SourceBgra, 0);

                    var displayed = await ReadPixelsAsync(frame, ColorManagementMode.ColorManageToSRgb);
                    expectedDisplayColor = displayed[..4];
                    AssertUniformColor(displayed, expectedDisplayColor, 1);
                    for (var channel = 0; channel < 3; channel++)
                    {
                        Assert.IsTrue(
                            Math.Abs(expectedDisplayColor[channel] - SourceBgra[channel]) >= 20,
                            "The fixture must exercise an embedded profile, rather than compare unmodified sRGB samples.");
                    }
                });

            // Explicit PNG/TIFF options require a fresh encoder. Use an isolated
            // settings model, without loading or changing the user's configuration.
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
                IgnoreOrientation = false,
                RemoveMetadata = false,
                PngInterlaceOption = PngInterlaceOption.On,
                TiffCompressOption = TiffCompressOption.None,
            };
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize
            {
                Name = "Test",
                Fit = ResizeFit.Stretch,
                Width = 32,
                Height = 16,
            });

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            await AssertEx.ImageAsync(
                outputDirectory.File(),
                async decoder =>
                {
                    var expectedCodec = extension == "png" ? BitmapDecoder.PngDecoderId : BitmapDecoder.TiffDecoderId;
                    Assert.AreEqual(expectedCodec, decoder.DecoderInformation.CodecId);
                    Assert.AreEqual(1u, decoder.FrameCount);
                    var frame = await decoder.GetFrameAsync(0);
                    Assert.AreEqual(32u, frame.PixelWidth);
                    Assert.AreEqual(16u, frame.PixelHeight);

                    // Both retaining the profile and converting samples to sRGB
                    // are valid: compare displayed colors, not ICC or sample bytes.
                    var displayed = await ReadPixelsAsync(frame, ColorManagementMode.ColorManageToSRgb);
                    AssertUniformColor(displayed, expectedDisplayColor, 3);
                });
        }

        private static async Task<byte[]> ReadPixelsAsync(BitmapFrame frame, ColorManagementMode mode)
        {
            var data = await frame.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                mode);
            var pixels = data.DetachPixelData();
            Assert.AreEqual((int)(frame.PixelWidth * frame.PixelHeight * 4), pixels.Length);
            return pixels;
        }

        private static void AssertUniformColor(byte[] pixels, byte[] color, int tolerance)
        {
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    Assert.IsTrue(
                        Math.Abs(pixels[offset + channel] - color[channel]) <= tolerance,
                        "Displayed color changed at pixel " + (offset / 4) + ", BGRA channel " + channel + ".");
                }
            }
        }

        private static byte[] CreateLinearRgbProfile()
        {
            // A self-contained ICC v2 RGB matrix profile: D50-adapted sRGB
            // colorants with gamma 1.0 curves. Midtone samples consequently have
            // visibly different values when converted to the sRGB display space.
            var curve = new byte[14];
            WriteSignature(curve, 0, "curv");
            WriteUInt32(curve, 8, 1);
            BinaryPrimitives.WriteUInt16BigEndian(curve.AsSpan(12), 0x0100);
            var description = Encoding.ASCII.GetBytes("PowerToys linear RGB\0");
            var descriptionTag = new byte[90 + description.Length];
            WriteSignature(descriptionTag, 0, "desc");
            WriteUInt32(descriptionTag, 8, (uint)description.Length);
            description.CopyTo(descriptionTag, 12);
            var copyright = Encoding.ASCII.GetBytes("Copyright (c) Microsoft Corporation\0");
            var copyrightTag = new byte[8 + copyright.Length];
            WriteSignature(copyrightTag, 0, "text");
            copyright.CopyTo(copyrightTag, 8);
            (string Signature, byte[] Data)[] tags =
            [
                ("desc", descriptionTag),
                ("cprt", copyrightTag),
                ("wtpt", CreateXyzTag(0x0000f6d6, 0x00010000, 0x0000d32d)),
                ("rXYZ", CreateXyzTag(0x00006fa0, 0x000038f5, 0x00000390)),
                ("gXYZ", CreateXyzTag(0x00006297, 0x0000b787, 0x000018d9)),
                ("bXYZ", CreateXyzTag(0x0000249f, 0x00000f84, 0x0000b6c3)),
                ("rTRC", curve),
                ("gTRC", curve),
                ("bTRC", curve),
            ];
            var dataOffset = 132 + (tags.Length * 12);
            var length = dataOffset;
            foreach (var tag in tags)
            {
                length += (tag.Data.Length + 3) & ~3;
            }

            var profile = new byte[length];
            WriteUInt32(profile, 0, (uint)length);
            WriteUInt32(profile, 8, 0x02100000);
            WriteSignature(profile, 12, "mntr");
            WriteSignature(profile, 16, "RGB ");
            WriteSignature(profile, 20, "XYZ ");
            BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(24), 2020);
            BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(26), 1);
            BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(28), 1);
            WriteSignature(profile, 36, "acsp");
            WriteSignature(profile, 40, "MSFT");
            WriteUInt32(profile, 68, 0x0000f6d6);
            WriteUInt32(profile, 72, 0x00010000);
            WriteUInt32(profile, 76, 0x0000d32d);
            WriteUInt32(profile, 128, (uint)tags.Length);
            for (var index = 0; index < tags.Length; index++)
            {
                var entry = 132 + (index * 12);
                WriteSignature(profile, entry, tags[index].Signature);
                WriteUInt32(profile, entry + 4, (uint)dataOffset);
                WriteUInt32(profile, entry + 8, (uint)tags[index].Data.Length);
                tags[index].Data.CopyTo(profile, dataOffset);
                dataOffset += (tags[index].Data.Length + 3) & ~3;
            }

            return profile;
        }

        private static byte[] CreateXyzTag(uint x, uint y, uint z)
        {
            var data = new byte[20];
            WriteSignature(data, 0, "XYZ ");
            WriteUInt32(data, 8, x);
            WriteUInt32(data, 12, y);
            WriteUInt32(data, 16, z);
            return data;
        }

        private static void WriteProfiledPng(string path, byte[] profile)
        {
            using var stream = File.Create(path);
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            WriteUInt32(header, 0, SourceWidth);
            WriteUInt32(header, 4, SourceHeight);
            header[8] = 8;
            header[9] = 2; // RGB, without conflicting sRGB or gAMA chunks.
            WritePngChunk(stream, "IHDR", header);
            var compressedProfile = Compress(profile);
            var prefix = Encoding.ASCII.GetBytes("Linear RGB\0\0");
            var iccp = new byte[prefix.Length + compressedProfile.Length];
            prefix.CopyTo(iccp, 0);
            compressedProfile.CopyTo(iccp, prefix.Length);
            WritePngChunk(stream, "iCCP", iccp);

            const int rowLength = 1 + (SourceWidth * 3);
            var samples = new byte[rowLength * SourceHeight];
            for (var row = 0; row < SourceHeight; row++)
            {
                var offset = (row * rowLength) + 1; // Filter method zero.
                for (var column = 0; column < SourceWidth; column++)
                {
                    samples[offset++] = SourceBgra[2];
                    samples[offset++] = SourceBgra[1];
                    samples[offset++] = SourceBgra[0];
                }
            }

            WritePngChunk(stream, "IDAT", Compress(samples));
            WritePngChunk(stream, "IEND", []);
        }

        private static byte[] Compress(byte[] data)
        {
            using var stream = new MemoryStream();
            using (var compression = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                compression.Write(data);
            }

            return stream.ToArray();
        }

        private static void WritePngChunk(Stream stream, string type, byte[] data)
        {
            var chunk = new byte[4 + data.Length];
            WriteSignature(chunk, 0, type);
            data.CopyTo(chunk, 4);
            Span<byte> integer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(integer, (uint)data.Length);
            stream.Write(integer);
            stream.Write(chunk);
            var crc = uint.MaxValue;
            foreach (var value in chunk)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
                }
            }

            BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc);
            stream.Write(integer);
        }

        private static void WriteProfiledTiff(string path, byte[] profile)
        {
            // Baseline, uncompressed RGB TIFF. Tag 34675 embeds the same profile
            // used by PNG, avoiding a dependency on host profiles or source files.
            const ushort entryCount = 14;
            const uint bitsOffset = 8 + 2 + (entryCount * 12) + 4;
            const uint xResolution = bitsOffset + 6;
            const uint yResolution = xResolution + 8;
            const uint profileOffset = yResolution + 8;
            var pixelOffset = profileOffset + (uint)profile.Length;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((byte)'I');
            writer.Write((byte)'I');
            writer.Write((ushort)42);
            writer.Write(8u);
            writer.Write(entryCount);
            WriteTiffEntry(writer, 256, 4, 1, SourceWidth);
            WriteTiffEntry(writer, 257, 4, 1, SourceHeight);
            WriteTiffEntry(writer, 258, 3, 3, bitsOffset);
            WriteTiffEntry(writer, 259, 3, 1, 1);
            WriteTiffEntry(writer, 262, 3, 1, 2);
            WriteTiffEntry(writer, 273, 4, 1, pixelOffset);
            WriteTiffEntry(writer, 277, 3, 1, 3);
            WriteTiffEntry(writer, 278, 4, 1, SourceHeight);
            WriteTiffEntry(writer, 279, 4, 1, SourceWidth * SourceHeight * 3);
            WriteTiffEntry(writer, 282, 5, 1, xResolution);
            WriteTiffEntry(writer, 283, 5, 1, yResolution);
            WriteTiffEntry(writer, 284, 3, 1, 1);
            WriteTiffEntry(writer, 296, 3, 1, 2);
            WriteTiffEntry(writer, 34675, 7, (uint)profile.Length, profileOffset);
            writer.Write(0u);
            writer.Write((ushort)8);
            writer.Write((ushort)8);
            writer.Write((ushort)8);
            writer.Write(96u);
            writer.Write(1u);
            writer.Write(96u);
            writer.Write(1u);
            writer.Write(profile);
            for (var pixel = 0; pixel < SourceWidth * SourceHeight; pixel++)
            {
                writer.Write(SourceBgra[2]);
                writer.Write(SourceBgra[1]);
                writer.Write(SourceBgra[0]);
            }
        }

        private static void WriteTiffEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(count);
            writer.Write(value);
        }

        private static void WriteSignature(byte[] bytes, int offset, string signature)
        {
            Encoding.ASCII.GetBytes(signature).CopyTo(bytes, offset);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
        }
    }
}
