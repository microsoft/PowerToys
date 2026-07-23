// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

using AdvancedPaste.Helpers;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.UnitTests.Mocks;
using AdvancedPaste.ViewModels;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class OpenAICompatibleProviderTests
{
    [TestMethod]
    public void ServiceType_RoundTripsThroughPersistedValues()
    {
        Assert.AreEqual(AIServiceType.OpenAICompatible, "OpenAICompatible".ToAIServiceType());
        Assert.AreEqual(AIServiceType.OpenAICompatible, "openai-compatible".ToAIServiceType());
        Assert.AreEqual("OpenAICompatible", AIServiceType.OpenAICompatible.ToConfigurationString());
        Assert.AreEqual("openaicompatible", AIServiceType.OpenAICompatible.ToNormalizedKey());
    }

    [TestMethod]
    public void ProviderDefaults_RequireExplicitConfiguration()
    {
        Assert.AreEqual(string.Empty, PasteAIProviderDefaults.GetDefaultModelName(AIServiceType.OpenAICompatible));

        var metadata = AIServiceTypeRegistry.GetMetadata(AIServiceType.OpenAICompatible);
        Assert.AreEqual("OpenAI Compatible", metadata.DisplayName);
        Assert.IsTrue(metadata.IsOnlineService);
        Assert.IsTrue(metadata.IsAvailableInUI);
    }

    [TestMethod]
    [DataRow("https://api.example.com/v1", true)]
    [DataRow("http://localhost:8080/v1", true)]
    [DataRow("  http://localhost:8080/v1  ", true)]
    [DataRow("http://user:password@localhost:8080/v1", false)]
    [DataRow("http://localhost:8080/v1#fragment", false)]
    [DataRow("ftp://example.com/v1", false)]
    [DataRow("example.com/v1", false)]
    [DataRow("", false)]
    public void EndpointValidation_AcceptsOnlySafeAbsoluteHttpEndpoints(string endpoint, bool expected)
    {
        Assert.AreEqual(expected, PasteAIProviderValidation.TryGetOpenAICompatibleEndpoint(endpoint, out _));
    }

    [TestMethod]
    [DataRow("test-model", true)]
    [DataRow("  test-model  ", true)]
    [DataRow("", false)]
    [DataRow("   ", false)]
    public void ModelValidation_RejectsEmptyNames(string modelName, bool expected)
    {
        Assert.AreEqual(expected, PasteAIProviderValidation.IsValidOpenAICompatibleModelName(modelName));
    }

    [TestMethod]
    public void ProviderFactory_UsesSemanticKernelProvider()
    {
        var provider = new PasteAIProviderFactory().CreateProvider(new PasteAIConfig
        {
            ProviderType = AIServiceType.OpenAICompatible,
            Model = "test-model",
            Endpoint = "https://api.example.com/v1",
            ApiKey = "test-key",
        });

        Assert.IsInstanceOfType(provider, typeof(SemanticKernelPasteProvider));
        var semanticKernelProvider = (SemanticKernelPasteProvider)provider;
        CollectionAssert.Contains(semanticKernelProvider.SupportedServiceTypes.ToArray(), AIServiceType.OpenAICompatible);
        Assert.IsTrue(AdvancedAIKernelService.IsServiceTypeSupported(AIServiceType.OpenAICompatible));
    }

    [TestMethod]
    public void SemanticKernelProvider_AllowsMissingCredential()
    {
        var provider = new SemanticKernelPasteProvider(new PasteAIConfig
        {
            ProviderType = AIServiceType.OpenAICompatible,
            Model = "test-model",
            Endpoint = "http://localhost:8080/v1",
            ApiKey = string.Empty,
        });
        var createKernel = typeof(SemanticKernelPasteProvider).GetMethod("CreateKernel", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(createKernel);
        Assert.IsNotNull(createKernel.Invoke(provider, null));
    }

    [TestMethod]
    public void AdvancedAIKernelService_AllowsMissingCredential()
    {
        var credentialsProvider = new Mock<IAICredentialsProvider>();
        credentialsProvider.Setup(provider => provider.GetKey()).Returns(string.Empty);
        var userSettings = new IntegrationTestUserSettings(
            AIServiceType.OpenAICompatible,
            "test-model",
            "http://localhost:8080/v1",
            moderationEnabled: false,
            providerId: "no-auth-provider");
        var service = new AdvancedAIKernelService(
            credentialsProvider.Object,
            new NoOpKernelQueryCacheService(),
            new Mock<IPromptModerationService>().Object,
            userSettings,
            new Mock<ICustomActionTransformService>().Object);
        var createKernel = typeof(KernelServiceBase).GetMethod("CreateKernel", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(createKernel);
        Assert.IsNotNull(createKernel.Invoke(service, null));
        credentialsProvider.Verify(provider => provider.Refresh(), Times.Once);
    }

    [TestMethod]
    public void AdvancedAIAvailability_DoesNotRequireCredentialForCompatibleProvider()
    {
        Assert.IsFalse(OptionsViewModel.RequiresConfiguredCredentialForAdvancedAI(AIServiceType.OpenAICompatible));
        Assert.IsTrue(OptionsViewModel.RequiresConfiguredCredentialForAdvancedAI(AIServiceType.OpenAI));
        Assert.IsTrue(OptionsViewModel.RequiresConfiguredCredentialForAdvancedAI(AIServiceType.AzureOpenAI));
    }

    [TestMethod]
    public void Provider_DoesNotUseClientModeration()
    {
        Assert.IsFalse(CustomActionTransformService.ShouldModerate(new PasteAIConfig
        {
            ProviderType = AIServiceType.OpenAICompatible,
            ModerationEnabled = true,
        }));

        Assert.IsTrue(CustomActionTransformService.ShouldModerate(new PasteAIConfig
        {
            ProviderType = AIServiceType.OpenAI,
            ModerationEnabled = true,
        }));
    }

    [TestMethod]
    public void ErrorTranslation_ProvidesSpecificCompatibleProviderMessages()
    {
        var invalidEndpoint = ErrorHelpers.TranslateOpenAICompatibleError(
            new System.InvalidOperationException("A valid HTTP or HTTPS endpoint is required for OpenAICompatible."),
            -1);
        var invalidModel = ErrorHelpers.TranslateOpenAICompatibleError(
            new System.InvalidOperationException("A model name is required for OpenAICompatible."),
            -1);
        var rejectedRequest = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), (int)HttpStatusCode.BadRequest);
        var authenticationFailed = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), (int)HttpStatusCode.Unauthorized);
        var rateLimited = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), (int)HttpStatusCode.TooManyRequests);
        var unavailableModel = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), (int)HttpStatusCode.ServiceUnavailable);
        var networkError = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), -1);
        const int GenericStatusCode = 418;
        var genericError = ErrorHelpers.TranslateOpenAICompatibleError(new HttpRequestException(), GenericStatusCode);
        var resourceLoader = ResourceLoaderInstance.ResourceLoader;

        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleEndpointInvalid"), invalidEndpoint);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleModelRequired"), invalidModel);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleRequestRejected"), rejectedRequest);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleAuthenticationFailed"), authenticationFailed);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleRateLimited"), rateLimited);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleServiceUnavailable"), unavailableModel);
        Assert.AreEqual(resourceLoader.GetString("OpenAICompatibleNetworkError"), networkError);
        StringAssert.Contains(genericError, GenericStatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void VaultEntries_AreIsolatedByProviderId()
    {
        var first = EnhancedVaultCredentialsProvider.BuildCredentialEntry(AIServiceType.OpenAICompatible, "provider-one");
        var second = EnhancedVaultCredentialsProvider.BuildCredentialEntry(AIServiceType.OpenAICompatible, "provider-two");

        Assert.IsTrue(first.HasValue);
        Assert.IsTrue(second.HasValue);
        Assert.AreEqual("PowerToys_AdvancedPaste_OpenAICompatible", first.Value.Resource);
        Assert.AreEqual(first.Value.Resource, second.Value.Resource);
        Assert.AreNotEqual(first.Value.Username, second.Value.Username);
        StringAssert.Contains(first.Value.Username, "openaicompatible");
    }

    [TestMethod]
    public void Configuration_SerializesWithoutCredential()
    {
        var provider = new PasteAIProviderDefinition
        {
            Id = "compatible-provider",
            ServiceTypeKind = AIServiceType.OpenAICompatible,
            ModelName = "test-model",
            EndpointUrl = "https://api.example.com/v1",
            ModerationEnabled = false,
            EnableAdvancedAI = true,
        };
        var configuration = new PasteAIConfiguration
        {
            ActiveProviderId = provider.Id,
            Providers = new ObservableCollection<PasteAIProviderDefinition> { provider },
        };

        var json = configuration.ToString();
        var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PasteAIConfiguration);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(AIServiceType.OpenAICompatible, roundTripped.ActiveProvider.ServiceTypeKind);
        Assert.AreEqual(provider.EndpointUrl, roundTripped.ActiveProvider.EndpointUrl);
        Assert.AreEqual(provider.ModelName, roundTripped.ActiveProvider.ModelName);
        Assert.IsFalse(json.Contains("api-key", System.StringComparison.OrdinalIgnoreCase));
    }
}
