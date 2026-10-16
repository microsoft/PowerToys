// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace ImageResizer.Utilities
{
    [TestClass]
    public class CodecHelperTests
    {
        [TestMethod]
        public void LegacyContainerFormatsMapToTheirEncoders()
        {
            Assert.AreEqual(BitmapEncoder.JpegEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057")));
            Assert.AreEqual(BitmapEncoder.PngEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf")));
            Assert.AreEqual(BitmapEncoder.BmpEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("0af1d87e-fcfe-4188-bdeb-a7906471cbe3")));
            Assert.AreEqual(BitmapEncoder.TiffEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3")));
            Assert.AreEqual(BitmapEncoder.JpegXREncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("57a37caa-367a-4540-916b-f183c5093a4b")));
            Assert.AreEqual(BitmapEncoder.GifEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(new Guid("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5")));
        }

        [TestMethod]
        public void UnknownContainerFormatFallsBackToPng()
        {
            Assert.AreEqual(BitmapEncoder.PngEncoderId, CodecHelper.GetEncoderIdFromLegacyGuid(Guid.Empty));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WmPhotoFallbackEncodesDecodableJpegXr(bool removeMetadata)
        {
            using var directory = new TestDirectory();
            var settings = FallbackSettings(new Guid("57a37caa-367a-4540-916b-f183c5093a4b"), removeMetadata);

            await new ResizeOperation("Test.ico", directory, settings).ExecuteAsync();

            var output = directory.File();
            Assert.AreEqual(".jxr", Path.GetExtension(output));
            await AssertEncodedImageAsync(output, BitmapDecoder.JpegXRDecoderId);
        }

        [TestMethod]
        public async Task PngFallbackStillEncodesDecodablePng()
        {
            using var directory = new TestDirectory();
            var settings = FallbackSettings(new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf"), removeMetadata: false);

            await new ResizeOperation("Test.ico", directory, settings).ExecuteAsync();

            var output = directory.File();
            Assert.AreEqual(".png", Path.GetExtension(output));
            await AssertEncodedImageAsync(output, BitmapDecoder.PngDecoderId);
        }

        private static Settings FallbackSettings(Guid fallbackEncoder, bool removeMetadata)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
                FallbackEncoder = fallbackEncoder,
                RemoveMetadata = removeMetadata,
            };
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize
            {
                Name = "Codec test",
                Width = 32,
                Height = 32,
                Fit = ResizeFit.Stretch,
            });
            return settings;
        }

        private static Task AssertEncodedImageAsync(string path, Guid expectedDecoder)
        {
            return AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    Assert.AreEqual(expectedDecoder, decoder.DecoderInformation.CodecId);
                    Assert.AreEqual(32u, decoder.PixelWidth);
                    Assert.AreEqual(32u, decoder.PixelHeight);
                    var pixels = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        new BitmapTransform(),
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);
                    Assert.AreEqual(32 * 32 * 4, pixels.DetachPixelData().Length);
                });
        }
    }
}
