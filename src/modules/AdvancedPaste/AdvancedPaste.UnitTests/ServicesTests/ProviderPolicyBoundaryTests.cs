// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class ProviderPolicyBoundaryTests
{
    [TestMethod]
    [DataRow("B", "B")]
    [DataRow(null, "A")]
    [DataRow("removed-provider", "A")]
    public async Task CustomAction_ForbiddenResolvedProvider_BlocksCredentialsModerationAndCreation(string requestedProvider, string resolvedProvider)
    {
        var fixture = new Fixture();
        PasteAIProviderDefinition checkedProvider = null;
        var service = fixture.CreateCustomService(provider =>
        {
            checkedProvider = provider;
            return false;
        });

        var exception = await Assert.ThrowsExceptionAsync<ProviderPolicyDisabledException>(() =>
            service.TransformAsync("prompt", "input", null, CancellationToken.None, null, providerIdOverride: requestedProvider));

        Assert.AreEqual("The selected AI provider is disabled by policy.", exception.Message);
        Assert.AreEqual(resolvedProvider, checkedProvider.Id);
        fixture.VerifyNoProviderAccess();
    }

    [TestMethod]
    [DataRow("B", "B")]
    [DataRow(null, "A")]
    [DataRow("removed-provider", "A")]
    public async Task AdvancedKernel_ForbiddenResolvedProvider_BlocksCredentialsAndCache(string requestedProvider, string resolvedProvider)
    {
        var fixture = new Fixture();
        PasteAIProviderDefinition checkedProvider = null;
        var cache = new Mock<IKernelQueryCacheService>(MockBehavior.Strict);
        var service = new AdvancedAIKernelService(
            fixture.Credentials.Object,
            cache.Object,
            fixture.Moderation.Object,
            fixture.Settings.Object,
            fixture.CreateCustomService(_ => true),
            provider =>
            {
                checkedProvider = provider;
                return false;
            });

        var exception = await Assert.ThrowsExceptionAsync<ProviderPolicyDisabledException>(() =>
            service.TransformClipboardAsync("query", DataPackageHelpers.CreateFromText("input").GetView(), false, CancellationToken.None, null, requestedProvider));

        Assert.AreEqual("The selected AI provider is disabled by policy.", exception.Message);
        Assert.AreEqual(resolvedProvider, checkedProvider.Id);
        fixture.VerifyNoProviderAccess();
        cache.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("B", "B")]
    [DataRow(null, "A")]
    [DataRow("removed-provider", "A")]
    public async Task CustomAction_AllowedResolvedProvider_UsesItsCredentialsAndConfiguration(string requestedProvider, string resolvedProvider)
    {
        var fixture = new Fixture();
        var provider = new Mock<IPasteAIProvider>(MockBehavior.Strict);
        provider.Setup(instance => instance.ProcessPasteAsync(It.IsAny<PasteAIRequest>(), CancellationToken.None, null)).ReturnsAsync("result");
        fixture.Credentials.Setup(instance => instance.GetKey(AIServiceType.OpenAI, resolvedProvider)).Returns("test-key");
        fixture.Factory.Setup(instance => instance.CreateProvider(It.Is<PasteAIConfig>(config => config.Model == resolvedProvider && config.ApiKey == "test-key")))
            .Returns(provider.Object);
        fixture.Moderation.Setup(instance => instance.ValidateAsync(It.IsAny<string>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var service = fixture.CreateCustomService(candidate => candidate.Id == resolvedProvider);

        var result = await service.TransformAsync("prompt", "input", null, CancellationToken.None, null, providerIdOverride: requestedProvider);

        Assert.AreEqual("result", result.Content);
        fixture.Credentials.Verify(instance => instance.GetKey(AIServiceType.OpenAI, resolvedProvider), Times.Once());
        fixture.Credentials.VerifyNoOtherCalls();
        provider.Verify(instance => instance.ProcessPasteAsync(It.IsAny<PasteAIRequest>(), CancellationToken.None, null), Times.Once());
    }

    [TestMethod]
    public async Task CustomAction_AllowedOllama_DoesNotRequireCredentialsOrModeration()
    {
        var fixture = new Fixture();
        fixture.Configuration.Providers[1].ServiceTypeKind = AIServiceType.Ollama;
        fixture.Configuration.Providers[1].EnableAdvancedAI = false;
        var provider = new Mock<IPasteAIProvider>(MockBehavior.Strict);
        provider.Setup(instance => instance.ProcessPasteAsync(It.IsAny<PasteAIRequest>(), CancellationToken.None, null)).ReturnsAsync("local result");
        fixture.Factory.Setup(instance => instance.CreateProvider(It.Is<PasteAIConfig>(config => config.ProviderType == AIServiceType.Ollama && config.Model == "B" && config.ApiKey == string.Empty)))
            .Returns(provider.Object);

        var result = await fixture.CreateCustomService(candidate => candidate.Id == "B")
            .TransformAsync("prompt", "input", null, CancellationToken.None, null, providerIdOverride: "B");

        Assert.AreEqual("local result", result.Content);
        fixture.Credentials.VerifyNoOtherCalls();
        fixture.Moderation.VerifyNoOtherCalls();
        provider.Verify(instance => instance.ProcessPasteAsync(It.IsAny<PasteAIRequest>(), CancellationToken.None, null), Times.Once());
    }

    [TestMethod]
    public async Task CancelledCustomAction_DoesNotResolvePolicyOrAccessProvider()
    {
        var fixture = new Fixture();
        var service = fixture.CreateCustomService(_ => throw new AssertFailedException("Policy should not be evaluated after cancellation."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            service.TransformAsync("prompt", "input", null, cancellation.Token, null, providerIdOverride: "B"));

        fixture.VerifyNoProviderAccess();
    }

    [TestMethod]
    public async Task CancelledAdvancedKernel_DoesNotResolvePolicyOrAccessProvider()
    {
        var fixture = new Fixture();
        var cache = new Mock<IKernelQueryCacheService>(MockBehavior.Strict);
        var service = new AdvancedAIKernelService(
            fixture.Credentials.Object,
            cache.Object,
            fixture.Moderation.Object,
            fixture.Settings.Object,
            fixture.CreateCustomService(_ => true),
            _ => throw new AssertFailedException("Policy should not be evaluated after cancellation."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            service.TransformClipboardAsync("query", DataPackageHelpers.CreateFromText("input").GetView(), false, cancellation.Token, null, "B"));

        fixture.VerifyNoProviderAccess();
        cache.VerifyNoOtherCalls();
    }

    private sealed class Fixture
    {
        public Mock<IAICredentialsProvider> Credentials { get; } = new(MockBehavior.Strict);

        public Mock<IPromptModerationService> Moderation { get; } = new(MockBehavior.Strict);

        public Mock<IPasteAIProviderFactory> Factory { get; } = new(MockBehavior.Strict);

        public Mock<IUserSettings> Settings { get; } = new(MockBehavior.Strict);

        public PasteAIConfiguration Configuration { get; } = new()
        {
            ActiveProviderId = "A",
            Providers = new ObservableCollection<PasteAIProviderDefinition>
            {
                new() { Id = "A", ServiceTypeKind = AIServiceType.OpenAI, ModelName = "A", EnableAdvancedAI = true },
                new() { Id = "B", ServiceTypeKind = AIServiceType.OpenAI, ModelName = "B", EnableAdvancedAI = true },
            },
        };

        public Fixture()
        {
            Settings.Setup(instance => instance.PasteAIConfiguration).Returns(Configuration);
        }

        public CustomActionTransformService CreateCustomService(Func<PasteAIProviderDefinition, bool> isProviderAllowed) =>
            new(Moderation.Object, Factory.Object, Credentials.Object, Settings.Object, isProviderAllowed);

        public void VerifyNoProviderAccess()
        {
            Credentials.VerifyNoOtherCalls();
            Moderation.VerifyNoOtherCalls();
            Factory.VerifyNoOtherCalls();
        }
    }
}
