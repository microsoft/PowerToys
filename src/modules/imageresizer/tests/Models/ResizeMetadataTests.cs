// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeMetadataTests
    {
        private const string Title = "测试标题 — résumé";
        private const string Comment = "备注：彩色样本 — München";
        private const string CameraModel = "Metadata regression fixture";
        private const string CameraManufacturer = "PowerToys";

        private static readonly string[] Authors = ["测试作者", "Zoë Łukasz"];

        private static readonly string[] PersonalMetadataProperties =
        [
            "System.Author",
            "System.Title",
            "System.Comment",
            "System.Photo.DateTaken",
            "System.Photo.CameraModel",
            "System.Photo.CameraManufacturer",
        ];

        private static readonly string[] RenderingMetadataProperties =
        [
            "System.Photo.Orientation",
            "System.Image.ColorSpace",
        ];

        [TestMethod]
        public async Task ExecutePreservesJpegPersonalAndExifMetadata()
        {
            using var directory = new TestDirectory();
            var input = Path.Combine(directory, "source.jpg");
            await CreateJpegAsync(input);
            var expected = await ReadFixtureMetadataAsync(input);

            var output = await ResizeAsync(input, Path.Combine(directory, "output"), MetadataSettings());

            await AssertMetadataAsync(output, expected, removeMetadata: false);
        }

        [TestMethod]
        public async Task ExecuteStripsJpegPersonalMetadata()
        {
            using var directory = new TestDirectory();
            var input = Path.Combine(directory, "source.jpg");
            await CreateJpegAsync(input);
            var expected = await ReadFixtureMetadataAsync(input);

            var output = await ResizeAsync(input, Path.Combine(directory, "output"), MetadataSettings(removeMetadata: true));

            await AssertMetadataAsync(output, expected, removeMetadata: true);
        }

        [TestMethod]
        public async Task ExecuteKeepsMetadataWhileApplyingJpegQuality()
        {
            using var directory = new TestDirectory();
            var input = Path.Combine(directory, "source.jpg");
            await CreateJpegAsync(input, textured: true);
            var expected = await ReadFixtureMetadataAsync(input);
            var lowQuality = MetadataSettings();
            lowQuality.JpegQualityLevel = 15;
            lowQuality.SelectedSize.Width = 96;
            lowQuality.SelectedSize.Height = 96;
            var highQuality = MetadataSettings();
            highQuality.JpegQualityLevel = 95;
            highQuality.SelectedSize.Width = 96;
            highQuality.SelectedSize.Height = 96;

            var lowOutput = await ResizeAsync(input, Path.Combine(directory, "low"), lowQuality);
            var highOutput = await ResizeAsync(input, Path.Combine(directory, "high"), highQuality);

            Assert.IsTrue(new FileInfo(highOutput).Length > new FileInfo(lowOutput).Length, "Higher quality should retain more of the textured image data.");
            await AssertMetadataAsync(lowOutput, expected, removeMetadata: false);
            await AssertMetadataAsync(highOutput, expected, removeMetadata: false);
            await AssertEx.ImageAsync(lowOutput, decoder => Assert.AreEqual(96u, decoder.PixelWidth));
            await AssertEx.ImageAsync(highOutput, decoder => Assert.AreEqual(96u, decoder.PixelWidth));
        }

        [DataTestMethod]
        [DataRow(6, false)]
        [DataRow(6, true)]
        [DataRow(8, false)]
        [DataRow(8, true)]
        public async Task ExecutePreservesExifDisplayOrientation(int orientation, bool removeMetadata)
        {
            using var directory = new TestDirectory();
            var input = Path.Combine(directory, "source.jpg");
            await CreateJpegAsync(input, (ushort)orientation);
            var expected = await ReadFixtureMetadataAsync(input, (ushort)orientation);

            var output = await ResizeAsync(input, Path.Combine(directory, "output"), MetadataSettings(removeMetadata));

            await AssertMetadataAsync(output, expected, removeMetadata);
            await AssertEx.ImageAsync(
                output,
                async decoder =>
                {
                    Assert.AreEqual(48u, decoder.PixelWidth, "The encoded pixels should retain the source's physical orientation.");
                    Assert.AreEqual(24u, decoder.PixelHeight);
                    Assert.AreEqual(24u, decoder.OrientedPixelWidth);
                    Assert.AreEqual(48u, decoder.OrientedPixelHeight);
                    var provider = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                        new BitmapTransform(),
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage);
                    var pixels = provider.DetachPixelData();

                    // Source corners are red, green, blue, yellow in reading order.
                    // EXIF 6 rotates clockwise; EXIF 8 rotates counterclockwise.
                    var expectedCorners = orientation == 6
                        ? new[] { (20, 20, 240), (240, 20, 20), (230, 230, 20), (20, 230, 20) }
                        : new[] { (20, 230, 20), (230, 230, 20), (240, 20, 20), (20, 20, 240) };
                    AssertCorner(pixels, 24, 6, 12, expectedCorners[0]);
                    AssertCorner(pixels, 24, 18, 12, expectedCorners[1]);
                    AssertCorner(pixels, 24, 6, 36, expectedCorners[2]);
                    AssertCorner(pixels, 24, 18, 36, expectedCorners[3]);
                });
        }

        private static Settings MetadataSettings(bool removeMetadata = false)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
                RemoveMetadata = removeMetadata,
                IgnoreOrientation = false,
            };
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize
            {
                Name = "Metadata test",
                Width = 48,
                Height = 48,
                Fit = ResizeFit.Fit,
            });
            return settings;
        }

        private static async Task<string> ResizeAsync(string input, string outputDirectory, Settings settings)
        {
            await new ResizeOperation(input, outputDirectory, settings).ExecuteAsync();
            return Directory.EnumerateFiles(outputDirectory).Single();
        }

        private static async Task CreateJpegAsync(string path, ushort orientation = 1, bool textured = false)
        {
            uint width = textured ? 192u : 96u;
            uint height = width / 2;
            var pixels = new byte[width * height * 4];
            var random = new Random(1234);
            for (uint y = 0; y < height; y++)
            {
                for (uint x = 0; x < width; x++)
                {
                    var index = (int)(((y * width) + x) * 4);
                    if (textured)
                    {
                        pixels[index] = (byte)random.Next(256);
                        pixels[index + 1] = (byte)random.Next(256);
                        pixels[index + 2] = (byte)random.Next(256);
                    }
                    else
                    {
                        var color = (x < width / 2, y < height / 2) switch
                        {
                            (true, true) => (240, 20, 20),
                            (false, true) => (20, 230, 20),
                            (true, false) => (20, 20, 240),
                            _ => (230, 230, 20),
                        };
                        pixels[index] = (byte)color.Item3;
                        pixels[index + 1] = (byte)color.Item2;
                        pixels[index + 2] = (byte)color.Item1;
                    }

                    pixels[index + 3] = 255;
                }
            }

            using var stream = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(
                BitmapEncoder.JpegEncoderId,
                stream.AsRandomAccessStream(),
                new BitmapPropertySet
                {
                    ["ImageQuality"] = new BitmapTypedValue(1f, PropertyType.Single),
                });
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 96, 96, pixels);
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                ["System.Author"] = new BitmapTypedValue(Authors, PropertyType.StringArray),
                ["System.Title"] = new BitmapTypedValue(Title, PropertyType.String),
                ["System.Comment"] = new BitmapTypedValue(Comment, PropertyType.String),
                ["System.Photo.DateTaken"] = new BitmapTypedValue(new DateTimeOffset(2012, 3, 4, 5, 6, 7, TimeSpan.Zero), PropertyType.DateTime),
                ["System.Photo.CameraModel"] = new BitmapTypedValue(CameraModel, PropertyType.String),
                ["System.Photo.CameraManufacturer"] = new BitmapTypedValue(CameraManufacturer, PropertyType.String),
                ["System.Photo.Orientation"] = new BitmapTypedValue(orientation, PropertyType.UInt16),
                ["System.Image.ColorSpace"] = new BitmapTypedValue((ushort)1, PropertyType.UInt16),
            });
            await encoder.FlushAsync();
        }

        private static async Task<BitmapPropertySet> ReadFixtureMetadataAsync(string path, ushort orientation = 1)
        {
            BitmapPropertySet result = null;
            await AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    result = await decoder.BitmapProperties.GetPropertiesAsync(PersonalMetadataProperties.Concat(RenderingMetadataProperties));

                    // Some JPEG codecs accept System.Image.ColorSpace without exposing
                    // it in a subsequent property query. Orientation and all personal
                    // properties are required; color space is checked only when present.
                    foreach (var name in PersonalMetadataProperties.Append("System.Photo.Orientation"))
                    {
                        Assert.IsTrue(result.ContainsKey(name), $"The source fixture must contain {name} before testing preservation or removal.");
                    }

                    CollectionAssert.AreEqual(Authors, (string[])result["System.Author"].Value);
                    Assert.AreEqual(Title, result["System.Title"].Value);
                    Assert.AreEqual(Comment, result["System.Comment"].Value);
                    Assert.AreEqual(CameraModel, result["System.Photo.CameraModel"].Value);
                    Assert.AreEqual(CameraManufacturer, result["System.Photo.CameraManufacturer"].Value);
                    Assert.AreEqual(orientation, result["System.Photo.Orientation"].Value);
                });
            return result;
        }

        private static Task AssertMetadataAsync(string path, BitmapPropertySet expected, bool removeMetadata)
        {
            return AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
                    var actual = await decoder.BitmapProperties.GetPropertiesAsync(PersonalMetadataProperties.Concat(RenderingMetadataProperties));
                    foreach (var name in PersonalMetadataProperties)
                    {
                        if (removeMetadata)
                        {
                            Assert.IsFalse(actual.ContainsKey(name), $"{name} should be removed.");
                        }
                        else
                        {
                            AssertMetadataValue(expected, actual, name);
                        }
                    }

                    foreach (var name in RenderingMetadataProperties)
                    {
                        if (expected.ContainsKey(name))
                        {
                            AssertMetadataValue(expected, actual, name);
                        }
                    }
                });
        }

        private static void AssertMetadataValue(BitmapPropertySet expected, BitmapPropertySet actual, string name)
        {
            Assert.IsTrue(actual.ContainsKey(name), $"{name} should be preserved.");
            Assert.AreEqual(expected[name].Type, actual[name].Type, $"{name} should retain its metadata type.");
            if (expected[name].Value is string[] strings)
            {
                CollectionAssert.AreEqual(strings, (string[])actual[name].Value, $"{name} should retain every value.");
            }
            else
            {
                Assert.AreEqual(expected[name].Value, actual[name].Value, $"{name} should retain its value.");
            }
        }

        private static void AssertCorner(byte[] pixels, int width, int x, int y, (int R, int G, int B) expected)
        {
            var index = ((y * width) + x) * 4;
            Assert.AreEqual(expected.R, pixels[index + 2], 20, "The displayed red channel should follow EXIF orientation.");
            Assert.AreEqual(expected.G, pixels[index + 1], 20, "The displayed green channel should follow EXIF orientation.");
            Assert.AreEqual(expected.B, pixels[index], 20, "The displayed blue channel should follow EXIF orientation.");
        }
    }
}
