// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.UnitTests.Mocks;
using AdvancedPaste.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class TransformHelpersTests
{
    [TestMethod]
    public async Task TransformToJpgFileProducesJpegFileAndRespectsQuality()
    {
        var lowQualitySize = await GetJpgOutputFileSizeAsync(10);
        var highQualitySize = await GetJpgOutputFileSizeAsync(100);

        Assert.IsTrue(
            lowQualitySize < highQualitySize,
            $"Expected low quality output ({lowQualitySize} bytes) to be smaller than high quality output ({highQualitySize} bytes)");
    }

    private static async Task<ulong> GetJpgOutputFileSizeAsync(int jpgQuality)
    {
        var inputPackage = await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png");

        var outputPackage = await TransformHelpers.TransformAsync(PasteFormats.PasteAsJpgFile, inputPackage.GetView(), CancellationToken.None, new NoOpProgress(), jpgQuality);

        var outputItems = await outputPackage.GetView().GetStorageItemsAsync();
        Assert.AreEqual(1, outputItems.Count);
        var outputFile = outputItems.Single() as StorageFile;
        Assert.IsNotNull(outputFile);
        Assert.AreEqual(".jpg", outputFile.FileType, ignoreCase: true, CultureInfo.InvariantCulture);

        using (var readStream = await outputFile.OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(readStream);
            Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
        }

        var outputFileSize = (await outputFile.GetBasicPropertiesAsync()).Size;
        await outputPackage.GetView().TryCleanupAfterDelayAsync(TimeSpan.Zero);
        return outputFileSize;
    }
}
