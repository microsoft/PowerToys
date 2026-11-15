// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeEncodingOptionsTests
    {
        private const string TiffCopyright = "PowerToys regression copyright";

        private static readonly string[] CommentPropertyQuery = ["System.Comment"];

        [TestMethod]
        [DataRow(PngInterlaceOption.On, 1, false, false)]
        [DataRow(PngInterlaceOption.On, 1, false, true)]
        [DataRow(PngInterlaceOption.On, 1, true, false)]
        [DataRow(PngInterlaceOption.On, 1, true, true)]
        [DataRow(PngInterlaceOption.Off, 0, false, false)]
        [DataRow(PngInterlaceOption.Off, 0, false, true)]
        [DataRow(PngInterlaceOption.Off, 0, true, false)]
        [DataRow(PngInterlaceOption.Off, 0, true, true)]
        public async Task ExecuteAppliesPngInterlaceWithOrWithoutMetadata(PngInterlaceOption option, int expectedInterlace, bool removeMetadata, bool sourceInterlaced)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "source.png");

            // Include both source modes: an explicit Off must override an interlaced
            // source, rather than accidentally matching an encoder default of Off.
            await WritePngAsync(source, sourceInterlaced);
            Assert.AreEqual(sourceInterlaced ? 1 : 0, ReadPngInterlace(source));
            var settings = CreateSettings(removeMetadata);
            settings.PngInterlaceOption = option;

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            Assert.AreEqual(expectedInterlace, ReadPngInterlace(outputDirectory.File()));
            await AssertEx.ImageAsync(
                outputDirectory.File(),
                async decoder =>
                {
                    Assert.AreEqual(BitmapDecoder.PngDecoderId, decoder.DecoderInformation.CodecId);
                    await AssertFrameAsync(await decoder.GetFrameAsync(0), 32, 16, 255, 0, 0);
                });
        }

        [TestMethod]
        [DataRow(PngInterlaceOption.On, false)]
        [DataRow(PngInterlaceOption.Off, false)]
        [DataRow(PngInterlaceOption.On, true)]
        [DataRow(PngInterlaceOption.Off, true)]
        public async Task ExecutePreservesOrRemovesNativePngText(PngInterlaceOption option, bool removeMetadata)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "metadata.png");
            await WritePngAsync(source, interlaced: option == PngInterlaceOption.Off);
            var expected = new Dictionary<string, string>
            {
                { "Title", "PNG regression title" },
                { "Author", "PNG regression author" },
                { "Description", "PNG regression description" },
                { "Comment", "PNG regression comment" },
                { "Copyright", "PNG regression copyright" },
                { "Regression-Key", "Native text outside the known property list" },
            };
            InsertPngText(source, expected);
            var sourceText = ReadPngText(source);
            foreach (var property in expected)
            {
                Assert.AreEqual(property.Value, sourceText[property.Key], "The source must contain each text property.");
            }

            var settings = CreateSettings(removeMetadata);
            settings.PngInterlaceOption = option;

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            var outputText = ReadPngText(outputDirectory.File());
            foreach (var property in expected)
            {
                if (removeMetadata)
                {
                    Assert.IsFalse(outputText.ContainsKey(property.Key), "Personal PNG text must be stripped: " + property.Key);
                }
                else
                {
                    Assert.IsTrue(outputText.ContainsKey(property.Key), "PNG text must survive an explicit encoder option: " + property.Key);
                    Assert.AreEqual(property.Value, outputText[property.Key]);
                }
            }
        }

        [TestMethod]
        [DataRow(TiffCompressOption.None, 1, false)]
        [DataRow(TiffCompressOption.None, 1, true)]
        [DataRow(TiffCompressOption.Ccitt3, 3, false)]
        [DataRow(TiffCompressOption.Ccitt3, 3, true)]
        [DataRow(TiffCompressOption.Ccitt4, 4, false)]
        [DataRow(TiffCompressOption.Ccitt4, 4, true)]
        [DataRow(TiffCompressOption.Lzw, 5, false)]
        [DataRow(TiffCompressOption.Lzw, 5, true)]
        [DataRow(TiffCompressOption.Rle, 32773, false)]
        [DataRow(TiffCompressOption.Rle, 32773, true)]
        [DataRow(TiffCompressOption.Zip, 8, false)]
        [DataRow(TiffCompressOption.Zip, 8, true)]
        public async Task ExecuteAppliesTiffCompressionWithOrWithoutMetadata(TiffCompressOption compression, int expectedTag, bool removeMetadata)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "source.tif");
            WriteMonochromeTiff(source);
            CollectionAssert.AreEqual(new ushort[] { 1 }, ReadTiffShortTags(source, 258), "The CCITT and RLE fixture must be 1bpp.");
            Assert.AreEqual(TiffCopyright, ReadTiffCopyright(source));
            await AssertEx.ImageAsync(
                source,
                async decoder =>
                {
                    Assert.AreEqual(65u, decoder.PixelWidth);
                    Assert.AreEqual(33u, decoder.PixelHeight);
                    await AssertMonochromeSamplesAsync(await decoder.GetFrameAsync(0), 4, 12);
                });
            var settings = CreateSettings(removeMetadata);
            settings.TiffCompressOption = compression;

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            // TIFF file tags differ from the settings/WIC option enumeration:
            // e.g. LZW is option 4 but Compression tag 259 must contain 5.
            CollectionAssert.AreEqual(new ushort[] { (ushort)expectedTag }, ReadTiffShortTags(outputDirectory.File(), 259));
            Assert.AreEqual(removeMetadata ? null : TiffCopyright, ReadTiffCopyright(outputDirectory.File()));
            await AssertEx.ImageAsync(
                outputDirectory.File(),
                async decoder =>
                {
                    Assert.AreEqual(1u, decoder.FrameCount);
                    var frame = await decoder.GetFrameAsync(0);
                    Assert.AreEqual(32u, frame.PixelWidth);
                    Assert.AreEqual(16u, frame.PixelHeight);
                    await AssertMonochromeSamplesAsync(frame, 2, 6);
                });
        }

        [TestMethod]
        [DataRow("png")]
        [DataRow("tif")]
        public async Task ExecutePreservesSixteenBitSamplesWithExplicitOptions(string extension)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "sixteen-bit." + extension);
            const ushort sample = 0x1234;
            if (extension == "png")
            {
                WriteSixteenBitPng(source, sample);
            }
            else
            {
                WriteSixteenBitTiff(source, sample);
            }

            await AssertSixteenBitSamplesAsync(source, extension, 64, 32, sample);
            var settings = CreateSettings(removeMetadata: false);
            settings.PngInterlaceOption = PngInterlaceOption.On;
            settings.TiffCompressOption = TiffCompressOption.None;

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            await AssertSixteenBitSamplesAsync(outputDirectory.File(), extension, 32, 16, sample);
        }

        [TestMethod]
        [DataRow(TiffCompressOption.None, 1, false)]
        [DataRow(TiffCompressOption.None, 1, true)]
        [DataRow(TiffCompressOption.Zip, 8, false)]
        [DataRow(TiffCompressOption.Zip, 8, true)]
        public async Task ExecuteAppliesTiffOptionsToEveryPage(TiffCompressOption compression, int expectedTag, bool removeMetadata)
        {
            using var inputDirectory = new TestDirectory();
            using var outputDirectory = new TestDirectory();
            var source = Path.Combine(inputDirectory, "pages.tif");
            await WriteMultipageTiffAsync(source);

            // Prove page identities and metadata before exercising ResizeOperation.
            // Different dimensions also distinguish per-page percentages from reuse
            // of the first page's transform.
            await AssertEx.ImageAsync(
                source,
                async decoder =>
                {
                    Assert.AreEqual(2u, decoder.FrameCount);
                    var first = await decoder.GetFrameAsync(0);
                    var second = await decoder.GetFrameAsync(1);
                    await AssertFrameAsync(first, 64, 32, 255, 0, 0);
                    await AssertFrameAsync(second, 40, 80, 0, 0, 255);
                    await AssertCommentAsync(first, "First page comment");
                    await AssertCommentAsync(second, "Second page comment");
                });
            var settings = CreateSettings(removeMetadata);
            settings.TiffCompressOption = compression;
            settings.SelectedSize.Unit = ResizeUnit.Percent;
            settings.SelectedSize.Width = 50;
            settings.SelectedSize.Height = 50;

            await new ResizeOperation(source, outputDirectory, settings).ExecuteAsync();

            CollectionAssert.AreEqual(
                new ushort[] { (ushort)expectedTag, (ushort)expectedTag },
                ReadTiffShortTags(outputDirectory.File(), 259),
                "The selected compression must apply to each TIFF IFD.");
            await AssertEx.ImageAsync(
                outputDirectory.File(),
                async decoder =>
                {
                    Assert.AreEqual(2u, decoder.FrameCount);
                    var first = await decoder.GetFrameAsync(0);
                    var second = await decoder.GetFrameAsync(1);
                    await AssertFrameAsync(first, 32, 16, 255, 0, 0);
                    await AssertFrameAsync(second, 20, 40, 0, 0, 255);
                    await AssertCommentAsync(first, removeMetadata ? null : "First page comment");
                    await AssertCommentAsync(second, removeMetadata ? null : "Second page comment");
                });
        }

        private static Settings CreateSettings(bool removeMetadata)
        {
            // Construct an isolated settings model: never load or write user settings.
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
                IgnoreOrientation = false,
                RemoveMetadata = removeMetadata,
            };
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize
            {
                Name = "Test",
                Fit = ResizeFit.Stretch,
                Width = 32,
                Height = 16,
            });
            return settings;
        }

        private static async Task WritePngAsync(string path, bool interlaced)
        {
            using var stream = File.Create(path);
            var properties = new BitmapPropertySet
            {
                { "InterlaceOption", new BitmapTypedValue(interlaced, PropertyType.Boolean) },
            };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream(), properties);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 64, 32, 96, 96, SolidPixels(64, 32, 255, 0, 0));
            await encoder.FlushAsync();
        }

        private static async Task WriteMultipageTiffAsync(string path)
        {
            using var stream = File.Create(path);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, stream.AsRandomAccessStream());
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 64, 32, 96, 96, SolidPixels(64, 32, 255, 0, 0));
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                { "System.Comment", new BitmapTypedValue("First page comment", PropertyType.String) },
            });
            await encoder.GoToNextFrameAsync();
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 40, 80, 96, 96, SolidPixels(40, 80, 0, 0, 255));
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                { "System.Comment", new BitmapTypedValue("Second page comment", PropertyType.String) },
            });
            await encoder.FlushAsync();
        }

        private static byte[] SolidPixels(int width, int height, byte red, byte green, byte blue)
        {
            var pixels = new byte[width * height * 4];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = blue;
                pixels[offset + 1] = green;
                pixels[offset + 2] = red;
                pixels[offset + 3] = 255;
            }

            return pixels;
        }

        private static async Task AssertFrameAsync(BitmapFrame frame, uint width, uint height, byte red, byte green, byte blue)
        {
            Assert.AreEqual(width, frame.PixelWidth);
            Assert.AreEqual(height, frame.PixelHeight);
            var pixelData = await frame.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var pixels = pixelData.DetachPixelData();
            Assert.AreEqual((int)(width * height * 4), pixels.Length);
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                Assert.AreEqual(blue, pixels[offset], "Unexpected blue channel at pixel " + (offset / 4));
                Assert.AreEqual(green, pixels[offset + 1], "Unexpected green channel at pixel " + (offset / 4));
                Assert.AreEqual(red, pixels[offset + 2], "Unexpected red channel at pixel " + (offset / 4));
            }
        }

        private static async Task AssertCommentAsync(BitmapFrame frame, string expected)
        {
            var properties = await frame.BitmapProperties.GetPropertiesAsync(CommentPropertyQuery);
            if (expected == null)
            {
                Assert.IsFalse(properties.ContainsKey("System.Comment"), "Personal metadata must be stripped from every TIFF page.");
            }
            else
            {
                Assert.IsTrue(properties.ContainsKey("System.Comment"), "Comment metadata must be preserved on every TIFF page.");
                Assert.AreEqual(expected, (string)properties["System.Comment"].Value);
            }
        }

        private static async Task AssertMonochromeSamplesAsync(BitmapFrame frame, int first, int second)
        {
            var data = await frame.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var pixels = data.DetachPixelData();
            Assert.AreEqual((int)(frame.PixelWidth * frame.PixelHeight * 4), pixels.Length);
            var samples = new[] { (first, first, 0), (second, first, 255), (first, second, 255), (second, second, 0) };
            foreach (var (x, y, expected) in samples)
            {
                var offset = ((y * (int)frame.PixelWidth) + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    Assert.AreEqual((byte)expected, pixels[offset + channel], "Black/white polarity or row stride changed at " + x + "," + y);
                }
            }
        }

        private static async Task AssertSixteenBitSamplesAsync(string path, string extension, uint width, uint height, ushort sample)
        {
            if (extension == "png")
            {
                var bytes = File.ReadAllBytes(path);
                Assert.AreEqual(16, (int)bytes[24], "PNG sample depth must remain 16 bits.");
            }
            else
            {
                foreach (var bitDepth in ReadTiffShortTags(path, 258))
                {
                    Assert.AreEqual((ushort)16, bitDepth, "Every TIFF sample must remain 16 bits.");
                }
            }

            await AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    Assert.AreEqual(width, decoder.PixelWidth);
                    Assert.AreEqual(height, decoder.PixelHeight);
                    var data = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Rgba16,
                        BitmapAlphaMode.Ignore,
                        new BitmapTransform(),
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);
                    var pixels = data.DetachPixelData();
                    Assert.AreEqual((int)(width * height * 8), pixels.Length);
                    for (var offset = 0; offset < pixels.Length; offset += 8)
                    {
                        for (var channel = 0; channel < 3; channel++)
                        {
                            Assert.AreEqual(sample, ReadUInt16(pixels, offset + (channel * 2), littleEndian: true), "A 16-bit sample was quantized.");
                        }
                    }
                });
        }

        private static void WriteSixteenBitPng(string path, ushort sample)
        {
            // WinRT SetPixelData does not support Gray16. Write a valid grayscale
            // PNG directly; the decoder expands its 16-bit samples to Rgba16.
            using var stream = File.Create(path);
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            WritePngChunk(stream, "IHDR", new byte[] { 0, 0, 0, 64, 0, 0, 0, 32, 16, 0, 0, 0, 0 });
            using var compressed = new MemoryStream();
            using (var compressor = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var row = 0; row < 32; row++)
                {
                    compressor.WriteByte(0);
                    for (var column = 0; column < 64; column++)
                    {
                        compressor.WriteByte((byte)(sample >> 8));
                        compressor.WriteByte((byte)(sample & 255));
                    }
                }
            }

            WritePngChunk(stream, "IDAT", compressed.ToArray());
            WritePngChunk(stream, "IEND", Array.Empty<byte>());
        }

        private static void WritePngChunk(Stream stream, string type, byte[] data)
        {
            var chunk = new byte[data.Length + 4];
            Encoding.ASCII.GetBytes(type).CopyTo(chunk, 0);
            data.CopyTo(chunk, 4);
            WriteBigEndianUInt32(stream, (uint)data.Length);
            stream.Write(chunk);
            WriteBigEndianUInt32(stream, CalculateCrc32(chunk, 0, chunk.Length));
        }

        private static void WriteSixteenBitTiff(string path, ushort sample)
        {
            const uint width = 64;
            const uint height = 32;
            const ushort entryCount = 12;
            const uint xResolution = 8 + 2 + (entryCount * 12) + 4;
            const uint yResolution = xResolution + 8;
            const uint pixelOffset = yResolution + 8;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((byte)'I');
            writer.Write((byte)'I');
            writer.Write((ushort)42);
            writer.Write(8u);
            writer.Write(entryCount);
            WriteTiffEntry(writer, 256, 4, width);
            WriteTiffEntry(writer, 257, 4, height);
            WriteTiffEntry(writer, 258, 3, 16);
            WriteTiffEntry(writer, 259, 3, 1);
            WriteTiffEntry(writer, 262, 3, 1);
            WriteTiffEntry(writer, 273, 4, pixelOffset);
            WriteTiffEntry(writer, 277, 3, 1);
            WriteTiffEntry(writer, 278, 4, height);
            WriteTiffEntry(writer, 279, 4, width * height * 2);
            WriteTiffEntry(writer, 282, 5, xResolution);
            WriteTiffEntry(writer, 283, 5, yResolution);
            WriteTiffEntry(writer, 296, 3, 2);
            writer.Write(0u);
            writer.Write(96u);
            writer.Write(1u);
            writer.Write(96u);
            writer.Write(1u);
            for (var index = 0; index < width * height; index++)
            {
                writer.Write(sample);
            }
        }

        private static void InsertPngText(string path, Dictionary<string, string> text)
        {
            var bytes = File.ReadAllBytes(path);
            using var output = new MemoryStream();
            output.Write(bytes, 0, 33);
            foreach (var property in text)
            {
                var data = Encoding.Latin1.GetBytes("tEXt" + property.Key + '\0' + property.Value);
                WriteBigEndianUInt32(output, (uint)data.Length - 4);
                output.Write(data);
                WriteBigEndianUInt32(output, CalculateCrc32(data, 0, data.Length));
            }

            output.Write(bytes, 33, bytes.Length - 33);
            File.WriteAllBytes(path, output.ToArray());
        }

        private static Dictionary<string, string> ReadPngText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var text = new Dictionary<string, string>();
            for (var offset = 8; offset + 12 <= bytes.Length;)
            {
                var length = (int)ReadUInt32(bytes, offset, littleEndian: false);
                Assert.IsTrue(length >= 0 && (long)offset + length + 12 <= bytes.Length, "Incomplete PNG chunk.");
                var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                Assert.AreEqual(ReadUInt32(bytes, offset + 8 + length, littleEndian: false), CalculateCrc32(bytes, offset + 4, length + 4));
                if (type == "tEXt" || type == "zTXt" || type == "iTXt")
                {
                    var data = bytes[(offset + 8)..(offset + 8 + length)];
                    var separator = System.Array.IndexOf(data, (byte)0);
                    Assert.IsTrue(separator > 0, "PNG text must have a keyword.");
                    var keyword = Encoding.Latin1.GetString(data, 0, separator);
                    var valueOffset = separator + 1;
                    var compressed = type == "zTXt";
                    if (type == "zTXt")
                    {
                        Assert.AreEqual((byte)0, data[valueOffset++]);
                    }
                    else if (type == "iTXt")
                    {
                        Assert.IsTrue(data[valueOffset] <= 1, "Invalid international PNG text compression flag.");
                        compressed = data[valueOffset++] == 1;
                        Assert.AreEqual((byte)0, data[valueOffset++]);
                        var languageEnd = System.Array.IndexOf(data, (byte)0, valueOffset);
                        Assert.IsTrue(languageEnd >= valueOffset, "Missing PNG text language terminator.");
                        var translatedKeywordEnd = System.Array.IndexOf(data, (byte)0, languageEnd + 1);
                        Assert.IsTrue(translatedKeywordEnd > languageEnd, "Missing translated PNG keyword terminator.");
                        valueOffset = translatedKeywordEnd + 1;
                    }

                    using var value = new MemoryStream(data, valueOffset, data.Length - valueOffset);
                    var encoding = type == "iTXt" ? Encoding.UTF8 : Encoding.Latin1;
                    if (compressed)
                    {
                        using var decompressor = new ZLibStream(value, CompressionMode.Decompress);
                        using var reader = new StreamReader(decompressor, encoding);
                        text[keyword] = reader.ReadToEnd();
                    }
                    else
                    {
                        using var reader = new StreamReader(value, encoding);
                        text[keyword] = reader.ReadToEnd();
                    }
                }

                offset += length + 12;
            }

            return text;
        }

        private static void WriteBigEndianUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static int ReadPngInterlace(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.IsTrue(bytes.Length >= 33, "PNG must contain a complete IHDR chunk.");
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 13, 73, 72, 68, 82 }, bytes[8..16], "IHDR must be the first PNG chunk.");
            Assert.AreEqual(ReadUInt32(bytes, 29, littleEndian: false), CalculateCrc32(bytes, 12, 17), "IHDR checksum must be valid.");
            return bytes[28];
        }

        private static uint CalculateCrc32(byte[] bytes, int offset, int length)
        {
            var crc = uint.MaxValue;
            for (var index = offset; index < offset + length; index++)
            {
                crc ^= bytes[index];
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
                }
            }

            return ~crc;
        }

        private static void WriteMonochromeTiff(string path)
        {
            // A small baseline TIFF with packed 1bpp samples. WinRT SetPixelData has
            // no 1bpp format; write the uncompressed fixture directly so CCITT3/4 and
            // RLE receive legal input without adding another imaging dependency.
            const uint width = 65;
            const uint height = 33;
            const ushort entryCount = 13;
            const uint firstIfd = 8;
            const uint xResolution = firstIfd + 2 + (entryCount * 12) + 4;
            const uint yResolution = xResolution + 8;
            const uint copyrightOffset = yResolution + 8;
            const uint bytesPerRow = (width + 7) / 8;
            const uint pixelBytes = bytesPerRow * height;
            var copyright = Encoding.ASCII.GetBytes(TiffCopyright + '\0');
            var pixelOffset = (copyrightOffset + (uint)copyright.Length + 1) & ~1u;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((byte)'I');
            writer.Write((byte)'I');
            writer.Write((ushort)42);
            writer.Write(firstIfd);
            writer.Write(entryCount);
            WriteTiffEntry(writer, 256, 4, width);
            WriteTiffEntry(writer, 257, 4, height);
            WriteTiffEntry(writer, 258, 3, 1);
            WriteTiffEntry(writer, 259, 3, 1);
            WriteTiffEntry(writer, 262, 3, 1);
            WriteTiffEntry(writer, 273, 4, pixelOffset);
            WriteTiffEntry(writer, 277, 3, 1);
            WriteTiffEntry(writer, 278, 4, height);
            WriteTiffEntry(writer, 279, 4, pixelBytes);
            WriteTiffEntry(writer, 282, 5, xResolution);
            WriteTiffEntry(writer, 283, 5, yResolution);
            WriteTiffEntry(writer, 296, 3, 2);
            WriteTiffEntry(writer, 33432, 2, copyrightOffset, (uint)copyright.Length);
            writer.Write(0u);
            writer.Write(96u);
            writer.Write(1u);
            writer.Write(96u);
            writer.Write(1u);
            writer.Write(copyright);
            if (writer.BaseStream.Position < pixelOffset)
            {
                writer.Write((byte)0);
            }

            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < bytesPerRow; column++)
                {
                    writer.Write((byte)(((row / 8) + column) % 2 == 0 ? 0 : 255));
                }
            }
        }

        private static void WriteTiffEntry(BinaryWriter writer, ushort tag, ushort type, uint value, uint count = 1)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(count);
            writer.Write(value);
        }

        private static string ReadTiffCopyright(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var littleEndian = bytes[0] == 'I';
            var offset = (int)ReadUInt32(bytes, 4, littleEndian);
            var entryCount = ReadUInt16(bytes, offset, littleEndian);
            for (var index = 0; index < entryCount; index++)
            {
                var entryOffset = offset + 2 + (index * 12);
                if (ReadUInt16(bytes, entryOffset, littleEndian) == 33432)
                {
                    Assert.AreEqual((ushort)2, ReadUInt16(bytes, entryOffset + 2, littleEndian), "Copyright must be an ASCII TIFF field.");
                    var length = (int)ReadUInt32(bytes, entryOffset + 4, littleEndian);
                    var valueOffset = length <= 4 ? entryOffset + 8 : (int)ReadUInt32(bytes, entryOffset + 8, littleEndian);
                    Assert.IsTrue(length > 0 && valueOffset >= 0 && (long)valueOffset + length <= bytes.Length, "Invalid TIFF Copyright value.");
                    return Encoding.ASCII.GetString(bytes, valueOffset, length).TrimEnd('\0');
                }
            }

            return null;
        }

        private static ushort[] ReadTiffShortTags(string path, ushort tag)
        {
            // Inspect the output bytes independently of BitmapEncoder options or
            // BitmapProperties, including every IFD in a multipage TIFF.
            var bytes = File.ReadAllBytes(path);
            Assert.IsTrue(bytes.Length >= 8, "Missing TIFF header.");
            var littleEndian = bytes[0] == 'I' && bytes[1] == 'I';
            Assert.IsTrue(littleEndian || (bytes[0] == 'M' && bytes[1] == 'M'), "Invalid TIFF byte order.");
            Assert.AreEqual((ushort)42, ReadUInt16(bytes, 2, littleEndian), "Expected a classic TIFF header.");
            var offset = ReadUInt32(bytes, 4, littleEndian);
            var visited = new HashSet<uint>();
            var values = new List<ushort>();
            while (offset != 0)
            {
                Assert.IsTrue(visited.Add(offset), "TIFF IFD chain must not loop.");
                Assert.IsTrue(offset <= bytes.Length - 2, "IFD offset is outside the TIFF.");
                var entryCount = ReadUInt16(bytes, (int)offset, littleEndian);
                var nextIfdOffset = (long)offset + 2 + (entryCount * 12);
                Assert.IsTrue(nextIfdOffset + 4 <= bytes.Length, "Incomplete TIFF IFD.");
                ushort[] fieldValues = null;
                for (var index = 0; index < entryCount; index++)
                {
                    var entryOffset = (int)offset + 2 + (index * 12);
                    if (ReadUInt16(bytes, entryOffset, littleEndian) == tag)
                    {
                        Assert.IsNull(fieldValues, "A TIFF IFD must not repeat the tested tag.");
                        Assert.AreEqual((ushort)3, ReadUInt16(bytes, entryOffset + 2, littleEndian), "Expected a SHORT TIFF tag.");
                        var count = ReadUInt32(bytes, entryOffset + 4, littleEndian);
                        Assert.IsTrue(count > 0 && count <= int.MaxValue, "Expected SHORT TIFF values.");
                        if (tag == 259)
                        {
                            Assert.AreEqual(1u, count, "Compression must contain one SHORT value per IFD.");
                        }

                        var valueOffset = count <= 2 ? entryOffset + 8 : (long)ReadUInt32(bytes, entryOffset + 8, littleEndian);
                        Assert.IsTrue(valueOffset + (count * 2L) <= bytes.Length, "Incomplete SHORT TIFF values.");
                        fieldValues = new ushort[(int)count];
                        for (var valueIndex = 0; valueIndex < fieldValues.Length; valueIndex++)
                        {
                            fieldValues[valueIndex] = ReadUInt16(bytes, (int)valueOffset + (valueIndex * 2), littleEndian);
                        }
                    }
                }

                Assert.IsNotNull(fieldValues, "TIFF IFD is missing tag " + tag);
                values.AddRange(fieldValues);
                offset = ReadUInt32(bytes, (int)nextIfdOffset, littleEndian);
            }

            Assert.IsTrue(values.Count > 0, "TIFF must contain at least one IFD.");
            return values.ToArray();
        }

        private static ushort ReadUInt16(byte[] bytes, int offset, bool littleEndian)
        {
            return littleEndian
                ? (ushort)(bytes[offset] | (bytes[offset + 1] << 8))
                : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        private static uint ReadUInt32(byte[] bytes, int offset, bool littleEndian)
        {
            return littleEndian
                ? (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24))
                : (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
        }
    }
}
