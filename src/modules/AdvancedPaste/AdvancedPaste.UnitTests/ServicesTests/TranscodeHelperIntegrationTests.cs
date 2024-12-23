// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.UnitTests.Mocks;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class TranscodeHelperIntegrationTests
{
    private sealed record class MediaProperties(BasicProperties Basic, MusicProperties Music, VideoProperties Video);

    private const string InputRootFolder = @"%USERPROFILE%\AdvancedPasteTranscodeMediaTestData";

    /// <summary> Tests transforming a folder of media files.
    /// - Verifies that the output file has the same basic properties (e.g. duration) as the input file.
    /// - Copies the output file to a subfolder of the input folder for manual inspection.
    /// </summary>
    [TestMethod]
    [DataRow(@"audio", PasteFormats.TranscodeToMp3)]
    [DataRow(@"video", PasteFormats.TranscodeToMp4)]
    public async Task TestTransformFolder(string inputSubfolder, PasteFormats format)
    {
        var inputFolder = Environment.ExpandEnvironmentVariables(Path.Combine(InputRootFolder, inputSubfolder));

        if (!Directory.Exists(inputFolder))
        {
            Assert.Inconclusive($"Skipping tests for {inputFolder} as it does not exist");
        }

        var outputPath = Path.Combine(inputFolder, $"test_output_{format}");

        foreach (var inputPath in Directory.EnumerateFiles(inputFolder))
        {
            await RunTestTransformFileAsync(inputPath, outputPath, format);
        }
    }

    private async Task RunTestTransformFileAsync(string inputPath, string finalOutputPath, PasteFormats format)
    {
        Logger.LogDebug($"Running {nameof(RunTestTransformFileAsync)} for {inputPath}/{format}");

        Directory.CreateDirectory(finalOutputPath);

        var inputPackage = await DataPackageHelpers.CreateFromFileAsync(inputPath);
        var inputProperties = await GetPropertiesAsync(await StorageFile.GetFileFromPathAsync(inputPath));

        var outputPackage = await TransformHelpers.TransformAsync(format, inputPackage.GetView(), CancellationToken.None, new NoOpProgress());

        var outputItems = await outputPackage.GetView().GetStorageItemsAsync();
        Assert.AreEqual(1, outputItems.Count);
        var outputFile = outputItems.Single() as StorageFile;
        Assert.IsNotNull(outputFile);
        var outputProperties = await GetPropertiesAsync(outputFile);
        AssertPropertiesMatch(format, inputProperties, outputProperties);

        await outputFile.CopyAsync(await StorageFolder.GetFolderFromPathAsync(finalOutputPath), outputFile.Name, NameCollisionOption.ReplaceExisting);
        await outputPackage.GetView().TryCleanupAfterDelayAsync(TimeSpan.Zero);
    }

    private static void AssertPropertiesMatch(PasteFormats format, MediaProperties inputProperties, MediaProperties outputProperties)
    {
        Assert.IsTrue(outputProperties.Basic.Size > 0);

        Assert.AreEqual(inputProperties.Music.Title, outputProperties.Music.Title);
        Assert.AreEqual(inputProperties.Music.Album, outputProperties.Music.Album);
        Assert.AreEqual(inputProperties.Music.Artist, outputProperties.Music.Artist);
        AssertDurationsApproxEqual(inputProperties.Music.Duration, outputProperties.Music.Duration);

        if (format == PasteFormats.TranscodeToMp4)
        {
            Assert.AreEqual(inputProperties.Video.Title, outputProperties.Video.Title);
            AssertDurationsApproxEqual(inputProperties.Video.Duration, outputProperties.Video.Duration);

            var inputVideoDimensions = GetNormalizedDimensions(inputProperties.Video);
            if (inputVideoDimensions != null)
            {
                Assert.AreEqual(inputVideoDimensions, GetNormalizedDimensions(outputProperties.Video));
            }
        }
    }

    private static async Task<MediaProperties> GetPropertiesAsync(StorageFile file) =>
        new(await file.GetBasicPropertiesAsync(), await file.Properties.GetMusicPropertiesAsync(), await file.Properties.GetVideoPropertiesAsync());

    private static void AssertDurationsApproxEqual(TimeSpan expected, TimeSpan actual) =>
        Assert.AreEqual(expected.Ticks, actual.Ticks, delta: TimeSpan.FromSeconds(1).Ticks);

    /// <summary>
    /// Gets the dimensions of a video, if available. Accounts for the fact that the dimensions may sometimes be swapped.
    /// </summary>
    private static (uint Width, uint Height)? GetNormalizedDimensions(VideoProperties properties) =>
        properties.Width == 0 || properties.Height == 0
            ? null
            : (Math.Max(properties.Width, properties.Height), Math.Min(properties.Width, properties.Height));
}
