// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.OpenAI;
using AdvancedPaste.Telemetry;
using AdvancedPaste.UnitTests.Helpers;
using AdvancedPaste.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests;

[Ignore("Test requires active OpenAI API key.")] // Comment out this line to run these tests after setting up OpenAI API key using AdvancedPaste Settings
[TestClass]

/// <summary>Integration tests for the Kernel service; connects to OpenAI and uses full AdvancedPaste action catalog.</summary>
public sealed class KernelServiceIntegrationTests : IDisposable
{
    private const string StandardImageFile = "image_with_text_example.png";
    private KernelService _kernelService;
    private AdvancedPasteEventListener _eventListener;

    [TestInitialize]
    public void TestInitialize()
    {
        VaultCredentialsProvider credentialsProvider = new();
        _kernelService = new KernelService(credentialsProvider, new CustomTextTransformService(credentialsProvider));
        _eventListener = new();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _eventListener?.Dispose();
    }

    [TestMethod]
    [DataRow("Translate to German", "What is that?", "Was ist das?", 600, new[] { PasteFormats.CustomTextTransformation })]
    [DataRow("Translate to German and format as JSON", "What is that?", @"[\s*Was ist das\?\s*]", 600, new[] { PasteFormats.CustomTextTransformation, PasteFormats.Json })]
    public async Task TestTextToTextTransform(string inputInstructions, string clipboardText, string expectedOutputPattern, int? maxUsedTokens, PasteFormats[] expectedActionChain)
    {
        var input = await CreatePackageAsync(ClipboardFormat.Text, clipboardText);
        var output = await GetKernelOutputAsync(inputInstructions, input);

        var outputText = await output.GetTextOrEmptyAsync();

        Assert.IsTrue(Regex.IsMatch(outputText, expectedOutputPattern));
        Assert.IsTrue(_eventListener.TotalTokens <= (maxUsedTokens ?? int.MaxValue));
        AssertActionChainIs(expectedActionChain);
    }

    [TestMethod]
    [DataRow("Convert to text", StandardImageFile, "This is an image with text", new[] { PasteFormats.ImageToText })]
    [DataRow("How many words are here?", StandardImageFile, "6", new[] { PasteFormats.ImageToText, PasteFormats.CustomTextTransformation })]
    public async Task TestImageToTextTransform(string inputInstructions, string imagePath, string expectedOutputPattern, PasteFormats[] expectedActionChain)
    {
        var input = await CreatePackageAsync(ClipboardFormat.Image, imagePath);
        var output = await GetKernelOutputAsync(inputInstructions, input);

        var outputText = await output.GetTextOrEmptyAsync();

        Assert.IsTrue(Regex.IsMatch(outputText, expectedOutputPattern));
        AssertActionChainIs(expectedActionChain);
    }

    [TestMethod]
    [DataRow("Get me a TXT file", ClipboardFormat.Image, StandardImageFile, "This is an image with text", new[] { PasteFormats.ImageToText, PasteFormats.PasteAsTxtFile })]
    public async Task TestFileOutputTransform(string inputInstructions, ClipboardFormat inputFormat, string inputData, string expectedOutputPattern, PasteFormats[] expectedActionChain)
    {
        var input = await CreatePackageAsync(inputFormat, inputData);
        var output = await GetKernelOutputAsync(inputInstructions, input);

        var outputText = await ReadFileTextAsync(output);

        Assert.IsTrue(Regex.IsMatch(outputText, expectedOutputPattern));
        AssertActionChainIs(expectedActionChain);
    }

    [TestMethod]
    [DataRow("Make this image bigger", ClipboardFormat.Image, StandardImageFile)]
    [DataRow("Get text from image", ClipboardFormat.Text, "What's up?")]
    public async Task TestTransformFailure(string inputInstructions, ClipboardFormat inputFormat, string inputData)
    {
        var input = await CreatePackageAsync(inputFormat, inputData);
        try
        {
            await GetKernelOutputAsync(inputInstructions, input);
            Assert.Fail("Kernel should have thrown an exception");
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        _eventListener?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<DataPackage> CreatePackageAsync(ClipboardFormat format, string data)
    {
        return format switch
        {
            ClipboardFormat.Text => DataPackageHelpers.CreateFromText(data),
            ClipboardFormat.Image => await ResourceUtils.GetImageAssetAsDataPackageAsync(data),
            _ => throw new ArgumentException("Unsupported format", nameof(format)),
        };
    }

    private async Task<DataPackageView> GetKernelOutputAsync(string inputInstructions, DataPackage input)
    {
        var output = await _kernelService.TransformClipboardAsync(inputInstructions, input.GetView(), isSavedQuery: false);

        Assert.AreEqual(1, _eventListener.SemanticKernelEvents.Count);
        return output.GetView();
    }

    private static async Task<string> ReadFileTextAsync(DataPackageView package)
    {
        CollectionAssert.Contains(package.AvailableFormats.ToArray(), StandardDataFormats.StorageItems);
        var storageItems = await package.GetStorageItemsAsync();
        Assert.AreEqual(1, storageItems.Count);

        return await File.ReadAllTextAsync(storageItems.Single().Path);
    }

    private void AssertActionChainIs(PasteFormats[] expectedActionChain) => Assert.AreEqual(AdvancedPasteSemanticKernelFormatEvent.FormatActionChain(expectedActionChain), _eventListener.SemanticKernelEvents.Single().UsedActionChain);
}
