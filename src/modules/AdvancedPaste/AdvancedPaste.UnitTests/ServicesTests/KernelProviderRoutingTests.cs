// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class KernelProviderRoutingTests
{
    [TestMethod]
    [DataRow("B", "B")]
    [DataRow("removed-provider", "A")]
    public async Task FreshStandardTool_UsesResolvedProvider(string requestedProvider, string expectedProvider)
    {
        var fixture = new Fixture(provider => provider.Id == expectedProvider);
        fixture.SetCompletion(kernel => InvokeStandardToolAsync(kernel, "tool prompt"));

        var output = await fixture.TransformAsync("query", requestedProvider);

        Assert.AreEqual(expectedProvider, await output.GetView().GetTextAsync());
        fixture.VerifyProvider(expectedProvider, "tool prompt", Times.Once());
    }

    [TestMethod]
    public async Task SavedCustomActionTool_UsesSelectedProviderInsteadOfActiveProvider()
    {
        var fixture = new Fixture(provider => provider.Id == "B");
        fixture.SetCompletion(kernel => kernel.InvokeAsync("Actions", "SavedAction"));

        var output = await fixture.TransformAsync("saved query", "B");

        Assert.AreEqual("B", await output.GetView().GetTextAsync());
        fixture.VerifyProvider("B", "saved prompt", Times.Once());
    }

    [TestMethod]
    public async Task CachedSavedActionChain_UsesCurrentInvocationProvider()
    {
        var fixture = new Fixture();
        fixture.SetCompletion(kernel => kernel.InvokeAsync("Actions", "SavedAction"));

        var firstOutput = await fixture.TransformAsync("same query", "A");
        Assert.AreEqual("A", await firstOutput.GetView().GetTextAsync());

        fixture.Settings.Setup(settings => settings.CustomActions).Returns([]);
        var cachedOutput = await fixture.TransformAsync("same query", "B");

        Assert.AreEqual("B", await cachedOutput.GetView().GetTextAsync());
        fixture.VerifyProvider("A", "saved prompt", Times.Once());
        fixture.VerifyProvider("B", "saved prompt", Times.Once());
        fixture.VerifyCompletion(Times.Once());
    }

    [TestMethod]
    public async Task ConcurrentInvocations_KeepSelectedProvidersIsolated()
    {
        var fixture = new Fixture();
        var bothEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int entered = 0;
        fixture.SetCompletion(async kernel =>
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothEntered.SetResult(true);
            }

            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await InvokeStandardToolAsync(kernel, "concurrent prompt");
        });

        var first = fixture.TransformAsync("query A", "A");
        var second = fixture.TransformAsync("query B", "B");
        var outputs = await Task.WhenAll(first, second);

        Assert.AreEqual("A", await outputs[0].GetView().GetTextAsync());
        Assert.AreEqual("B", await outputs[1].GetView().GetTextAsync());
        fixture.VerifyProvider("A", "concurrent prompt", Times.Once());
        fixture.VerifyProvider("B", "concurrent prompt", Times.Once());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ForbiddenNestedProvider_CannotBeRecoveredByAssistantResponseOrLaterTool(bool runLaterTool)
    {
        var fixture = new Fixture(_ => false);
        fixture.SetCompletion(
            async kernel =>
            {
                try
                {
                    await InvokeStandardToolAsync(kernel, "tool prompt");
                }
                catch (Exception)
                {
                    // Simulate the model handling a failed tool call and returning a final answer.
                }

                if (runLaterTool)
                {
                    try
                    {
                        await kernel.InvokeAsync("Actions", nameof(PasteFormats.PlainText));
                    }
                    catch (Exception)
                    {
                        // A later successful transform must not clear the policy denial.
                    }
                }
            },
            "assistant response");

        await Assert.ThrowsExceptionAsync<ProviderPolicyDisabledException>(() => fixture.TransformAsync("query", "B"));

        fixture.VerifyNoProviderAccess();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ForbiddenNestedProvider_RemainsTerminalWithEarlierOutputOrAnotherToolError(bool overwriteLastError)
    {
        var fixture = new Fixture(_ => false);
        fixture.SetCompletion(
            async kernel =>
            {
                await kernel.InvokeAsync("Actions", nameof(PasteFormats.PlainText));
                try
                {
                    await InvokeStandardToolAsync(kernel, "tool prompt");
                }
                catch (Exception)
                {
                    // Simulate the model handling the failed call after a tool already produced usable output.
                }

                if (overwriteLastError)
                {
                    // An already-running tool can finish with another error after the policy denial.
                    kernel.SetLastError(new InvalidOperationException("Another tool failed."));
                }
            },
            "assistant response");

        await Assert.ThrowsExceptionAsync<ProviderPolicyDisabledException>(() => fixture.TransformAsync("query", "B"));

        fixture.VerifyNoProviderAccess();
    }

    private static async Task InvokeStandardToolAsync(Kernel kernel, string prompt) =>
        await kernel.InvokeAsync("Actions", nameof(PasteFormats.CustomTextTransformation), new KernelArguments { ["prompt"] = prompt });

    private sealed class Fixture
    {
        private readonly Mock<IAICredentialsProvider> credentials = new(MockBehavior.Strict);
        private readonly Mock<IPasteAIProviderFactory> providerFactory = new(MockBehavior.Strict);
        private readonly Mock<IChatCompletionService> completion = new(MockBehavior.Strict);
        private readonly ConcurrentBag<(string ProviderId, string Prompt)> requests = [];
        private readonly TestKernelService service;

        public Mock<IUserSettings> Settings { get; } = new();

        public Fixture(Func<PasteAIProviderDefinition, bool> isProviderAllowed = null)
        {
            Settings.Setup(settings => settings.PasteAIConfiguration).Returns(new PasteAIConfiguration
            {
                ActiveProviderId = "A",
                Providers = new ObservableCollection<PasteAIProviderDefinition>
                {
                    new() { Id = "A", ServiceTypeKind = AIServiceType.OpenAI, ModelName = "A", EnableAdvancedAI = true, ModerationEnabled = false },
                    new() { Id = "B", ServiceTypeKind = AIServiceType.AzureOpenAI, ModelName = "B", EnableAdvancedAI = true, ModerationEnabled = false },
                },
            });
            Settings.Setup(settings => settings.CustomActions).Returns(
                [new AdvancedPasteCustomAction { Id = 1, Name = "SavedAction", Prompt = "saved prompt" }]);

            credentials.Setup(provider => provider.GetKey(It.IsAny<AIServiceType>(), It.IsAny<string>())).Returns("test-key");
            providerFactory.Setup(factory => factory.CreateProvider(It.IsAny<PasteAIConfig>()))
                .Returns((PasteAIConfig config) =>
                {
                    var provider = new Mock<IPasteAIProvider>(MockBehavior.Strict);
                    provider.Setup(instance => instance.ProcessPasteAsync(It.IsAny<PasteAIRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<double>>()))
                        .Callback((PasteAIRequest request, CancellationToken token, IProgress<double> progress) => requests.Add((config.Model, request.Prompt)))
                        .ReturnsAsync(config.Model);
                    return provider.Object;
                });

            var moderation = new Mock<IPromptModerationService>(MockBehavior.Strict);
            var transform = new CustomActionTransformService(moderation.Object, providerFactory.Object, credentials.Object, Settings.Object, isProviderAllowed ?? (_ => true));
            var cache = new Mock<IKernelQueryCacheService>();
            CacheValue cachedValue = null;
            cache.Setup(instance => instance.ReadOrNull(It.IsAny<CacheKey>())).Returns(() => cachedValue);
            cache.Setup(instance => instance.WriteAsync(It.IsAny<CacheKey>(), It.IsAny<CacheValue>()))
                .Callback((CacheKey key, CacheValue value) => cachedValue = value)
                .Returns(Task.CompletedTask);

            service = new TestKernelService(cache.Object, moderation.Object, Settings.Object, transform, completion.Object);
        }

        public void SetCompletion(Func<Kernel, Task> invokeTool, string assistantResponse = "")
        {
            completion.Setup(instance => instance.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
                .Returns(async (ChatHistory history, PromptExecutionSettings settings, Kernel kernel, CancellationToken token) =>
                {
                    await invokeTool(kernel);
                    return (IReadOnlyList<ChatMessageContent>)[new(AuthorRole.Assistant, assistantResponse)];
                });
        }

        public Task<DataPackage> TransformAsync(string prompt, string providerId) =>
            service.TransformClipboardAsync(prompt, DataPackageHelpers.CreateFromText("input").GetView(), true, CancellationToken.None, null, providerId);

        public void VerifyProvider(string providerId, string prompt, Times times)
        {
            var serviceType = providerId == "A" ? AIServiceType.OpenAI : AIServiceType.AzureOpenAI;
            credentials.Verify(instance => instance.GetKey(serviceType, providerId), times);
            providerFactory.Verify(instance => instance.CreateProvider(It.Is<PasteAIConfig>(config => config.Model == providerId && config.ProviderType == serviceType)), times);
            Assert.IsTrue(requests.Any(request => request.ProviderId == providerId && request.Prompt == prompt));
        }

        public void VerifyCompletion(Times times) =>
            completion.Verify(
                instance => instance.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()),
                times);

        public void VerifyNoProviderAccess()
        {
            credentials.VerifyNoOtherCalls();
            providerFactory.VerifyNoOtherCalls();
        }
    }

    private sealed class TestKernelService(
        IKernelQueryCacheService cache,
        IPromptModerationService moderation,
        IUserSettings settings,
        ICustomActionTransformService transform,
        IChatCompletionService completion) : KernelServiceBase(cache, moderation, settings, transform)
    {
        protected override void AddChatCompletionService(IKernelBuilder builder, IKernelRuntimeConfiguration configuration) =>
            builder.Services.AddKeyedSingleton(configuration.ModelName, completion);

        protected override PromptExecutionSettings GetPromptExecutionSettings(IKernelRuntimeConfiguration configuration) => new();

        protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent message) => AIServiceUsage.None;

        protected override IKernelRuntimeConfiguration GetRuntimeConfiguration(string providerIdOverride)
        {
            Assert.IsTrue(AdvancedAIProviderResolver.TryResolveAdvancedProvider(UserSettings.PasteAIConfiguration, providerIdOverride, out var provider));
            return new RuntimeConfiguration(provider.Id, provider.ServiceTypeKind, provider.ModelName);
        }
    }

    private sealed record RuntimeConfiguration(string ProviderId, AIServiceType ServiceType, string ModelName) : IKernelRuntimeConfiguration
    {
        public string Endpoint => null;

        public string DeploymentName => null;

        public string ModelPath => null;

        public string SystemPrompt => null;

        public bool ModerationEnabled => false;
    }
}
