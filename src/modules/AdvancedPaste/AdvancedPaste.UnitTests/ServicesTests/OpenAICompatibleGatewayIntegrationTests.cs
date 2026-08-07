// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Services.OpenAI;
using AdvancedPaste.UnitTests.Mocks;
using AdvancedPaste.UnitTests.Utils;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class OpenAICompatibleGatewayIntegrationTests
{
    private const string ProviderId = "integration-openaicompatible";

    private EnhancedVaultCredentialsProvider _credentialsProvider;
    private ICustomActionTransformService _customActionTransformService;
    private IKernelService _kernelService;
    private RecordingKernelQueryCacheService _queryCacheService;

    [TestInitialize]
    public void TestInitialize()
    {
        string endpoint = Environment.GetEnvironmentVariable("POWERTOYS_OPENAI_COMPATIBLE_ENDPOINT");
        string model = Environment.GetEnvironmentVariable("POWERTOYS_OPENAI_COMPATIBLE_MODEL");

        if (!PasteAIProviderValidation.TryGetOpenAICompatibleEndpoint(endpoint, out _) ||
            !PasteAIProviderValidation.IsValidOpenAICompatibleModelName(model))
        {
            Assert.Inconclusive("Set POWERTOYS_OPENAI_COMPATIBLE_ENDPOINT and POWERTOYS_OPENAI_COMPATIBLE_MODEL to run live Gateway tests.");
        }

        IntegrationTestUserSettings userSettings = new(
            AIServiceType.OpenAICompatible,
            model,
            endpoint,
            moderationEnabled: false,
            providerId: ProviderId);
        _credentialsProvider = new EnhancedVaultCredentialsProvider(userSettings);

        PromptModerationService moderationService = new(_credentialsProvider);
        PasteAIProviderFactory providerFactory = new();
        _customActionTransformService = new CustomActionTransformService(moderationService, providerFactory, _credentialsProvider, userSettings);
        _queryCacheService = new RecordingKernelQueryCacheService();
        _kernelService = new AdvancedAIKernelService(_credentialsProvider, _queryCacheService, moderationService, userSettings, _customActionTransformService);
    }

    [TestMethod]
    [TestCategory("LiveGateway")]
    [DataRow("Translate to German", "What is that?", "Was ist das\\?")]
    [DataRow("Summarize in one short sentence", "PowerToys is a set of utilities for Windows power users. It helps users customize workflows and improve productivity.", "PowerToys")]
    [DataRow("Convert to a JSON object with lowercase keys. Output only JSON.", "Name: Alice\nDepartment: Engineering", "\\\"name\\\"\\s*:\\s*\\\"Alice\\\"")]
    public async Task CustomAction_TransformsText(string prompt, string input, string expectedPattern)
    {
        var result = await _customActionTransformService.TransformAsync(
            prompt,
            input,
            null,
            CancellationToken.None,
            new NoOpProgress());

        Assert.IsTrue(Regex.IsMatch(result.Content, expectedPattern, RegexOptions.IgnoreCase));
        Assert.IsTrue(result.Usage.HasUsage);
    }

    [TestMethod]
    [TestCategory("LiveGateway")]
    public async Task CustomAction_TransformsImage()
    {
        var input = await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png");
        var imageBytes = await input.GetView().GetImageAsPngBytesAsync();

        var result = await _customActionTransformService.TransformAsync(
            "Extract the text from this image",
            null,
            imageBytes,
            CancellationToken.None,
            new NoOpProgress());

        StringAssert.Contains(result.Content, "This is an image with text");
        Assert.IsTrue(result.Usage.HasUsage);
    }

    [TestMethod]
    [TestCategory("LiveGateway")]
    public async Task AdvancedAI_InvokesTools()
    {
        var input = DataPackageHelpers.CreateFromText("What is that?");

        var output = await _kernelService.TransformClipboardAsync(
            "Translate to German and format as JSON",
            input.GetView(),
            isSavedQuery: false,
            CancellationToken.None,
            new NoOpProgress());
        var outputText = await output.GetView().GetTextOrEmptyAsync();

        Assert.IsTrue(Regex.IsMatch(outputText, "Was ist das\\?", RegexOptions.IgnoreCase));
        Assert.IsNotNull(_queryCacheService.WrittenValue);
        CollectionAssert.Contains(
            _queryCacheService.WrittenValue.ActionChain.Select(item => item.Format).ToArray(),
            PasteFormats.CustomTextTransformation);
    }

    private sealed class RecordingKernelQueryCacheService : IKernelQueryCacheService
    {
        public CacheValue WrittenValue { get; private set; }

        public CacheValue ReadOrNull(CacheKey cacheKey) => null;

        public Task WriteAsync(CacheKey cacheKey, CacheValue value)
        {
            WrittenValue = value;
            return Task.CompletedTask;
        }
    }
}
