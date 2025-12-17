// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;

namespace AdvancedPaste.Services;

public sealed class AdvancedAIKernelService : KernelServiceBase
{
    private sealed record RuntimeConfiguration(
        AIServiceType ServiceType,
        string ModelName,
        string Endpoint,
        string DeploymentName,
        string ModelPath,
        string SystemPrompt,
        bool ModerationEnabled) : IKernelRuntimeConfiguration;

    private readonly IAICredentialsProvider credentialsProvider;

    public AdvancedAIKernelService(
        IAICredentialsProvider credentialsProvider,
        IKernelQueryCacheService queryCacheService,
        IPromptModerationService promptModerationService,
        IUserSettings userSettings,
        ICustomActionTransformService customActionTransformService)
        : base(queryCacheService, promptModerationService, userSettings, customActionTransformService)
    {
        ArgumentNullException.ThrowIfNull(credentialsProvider);

        this.credentialsProvider = credentialsProvider;
    }

    protected override string AdvancedAIModelName => GetRuntimeConfiguration().ModelName;

    protected override PromptExecutionSettings PromptExecutionSettings => CreatePromptExecutionSettings();

    protected override void AddAIServices(IKernelBuilder kernelBuilder)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);

        // 1. Register the primary Chat Completion Service
        RegisterChatService(kernelBuilder);

        // 2. Register auxiliary services (e.g., TextToImage) by searching through all configured providers
        RegisterAuxiliaryServices(kernelBuilder);
    }

    private void RegisterChatService(IKernelBuilder kernelBuilder)
    {
        var runtimeConfig = GetRuntimeConfiguration();
        var serviceType = runtimeConfig.ServiceType;
        var modelName = runtimeConfig.ModelName;
        var apiKey = GetApiKey(serviceType);

        var endpoint = string.IsNullOrWhiteSpace(runtimeConfig.Endpoint) ? null : runtimeConfig.Endpoint.Trim();
        var deployment = string.IsNullOrWhiteSpace(runtimeConfig.DeploymentName) ? modelName : runtimeConfig.DeploymentName;

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                kernelBuilder.AddOpenAIChatCompletion(modelName, apiKey, serviceId: modelName);
                break;
            case AIServiceType.AzureOpenAI:
                kernelBuilder.AddAzureOpenAIChatCompletion(deployment, RequireEndpoint(endpoint, serviceType), apiKey, serviceId: modelName);
                break;
            default:
                throw new NotSupportedException($"Service type '{runtimeConfig.ServiceType}' is not supported");
        }
    }

    private void RegisterAuxiliaryServices(IKernelBuilder kernelBuilder)
    {
        // Try to find a dedicated Image Generation provider
        if (TryRegisterImageService(kernelBuilder))
        {
            return;
        }
    }

    private bool TryRegisterImageService(IKernelBuilder kernelBuilder)
    {
        var allProviders = this.UserSettings.PasteAIConfiguration?.Providers;
        if (allProviders == null)
        {
            return false;
        }

        var imageProvider = allProviders.FirstOrDefault(p =>
            p.Capabilities.HasFlag(AIServiceCapability.TextToImage));

        if (imageProvider == null)
        {
            return false;
        }

        var serviceType = NormalizeServiceType(imageProvider.ServiceTypeKind);
        var apiKey = this.credentialsProvider.GetKey(imageProvider.Id, serviceType);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var endpoint = string.IsNullOrWhiteSpace(imageProvider.EndpointUrl) ? null : imageProvider.EndpointUrl.Trim();
        var deployment = string.IsNullOrWhiteSpace(imageProvider.DeploymentName) ? imageProvider.ModelName : imageProvider.DeploymentName;

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAITextToImage(apiKey, modelId: imageProvider.ModelName);
#pragma warning restore SKEXP0010
                return true;
            case AIServiceType.AzureOpenAI:
#pragma warning disable SKEXP0010
                kernelBuilder.AddAzureOpenAITextToImage(deployment, RequireEndpoint(endpoint, serviceType), apiKey);
#pragma warning restore SKEXP0010
                return true;
            default:
                return false;
        }
    }

    private string GetApiKey(AIServiceType serviceType)
    {
        if (!RequiresApiKey(serviceType))
        {
            return string.Empty;
        }

        this.credentialsProvider.Refresh();
        var apiKey = (this.credentialsProvider.GetKey() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"An API key is required for {serviceType} but none was found in the credential vault.");
        }

        return apiKey;
    }

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        return AIServiceUsageHelper.GetOpenAIServiceUsage(chatMessage);
    }

    protected override bool ShouldModerateAdvancedAI()
    {
        if (!TryGetRuntimeConfiguration(out var runtimeConfig))
        {
            return false;
        }

        return runtimeConfig.ModerationEnabled && (runtimeConfig.ServiceType == AIServiceType.OpenAI || runtimeConfig.ServiceType == AIServiceType.AzureOpenAI);
    }

    private static string GetModelName(PasteAIProviderDefinition config)
    {
        if (!string.IsNullOrWhiteSpace(config?.ModelName))
        {
            return config.ModelName;
        }

        return "gpt-4o";
    }

    protected override IKernelRuntimeConfiguration GetRuntimeConfiguration()
    {
        if (TryGetRuntimeConfiguration(out var runtimeConfig))
        {
            return runtimeConfig;
        }

        throw new InvalidOperationException("No Advanced AI provider is configured.");
    }

    private bool TryGetRuntimeConfiguration(out IKernelRuntimeConfiguration runtimeConfig)
    {
        runtimeConfig = null;

        if (!TryResolveAdvancedProvider(out var provider))
        {
            return false;
        }

        var serviceType = NormalizeServiceType(provider.ServiceTypeKind);
        if (!IsServiceTypeSupported(serviceType))
        {
            return false;
        }

        runtimeConfig = new RuntimeConfiguration(
            serviceType,
            GetModelName(provider),
            provider.EndpointUrl,
            provider.DeploymentName,
            provider.ModelPath,
            provider.SystemPrompt,
            provider.ModerationEnabled);
        return true;
    }

    private bool TryResolveAdvancedProvider(out PasteAIProviderDefinition provider)
    {
        provider = null;

        var configuration = this.UserSettings?.PasteAIConfiguration;
        if (configuration is null)
        {
            return false;
        }

        var activeProvider = configuration.ActiveProvider;
        if (IsAdvancedProvider(activeProvider))
        {
            provider = activeProvider;
            return true;
        }

        if (activeProvider is not null)
        {
            return false;
        }

        var fallback = configuration.Providers?.FirstOrDefault(IsAdvancedProvider);
        if (fallback is not null)
        {
            provider = fallback;
            return true;
        }

        return false;
    }

    private static bool IsAdvancedProvider(PasteAIProviderDefinition provider)
    {
        if (provider is null || !provider.EnableAdvancedAI)
        {
            return false;
        }

        var serviceType = NormalizeServiceType(provider.ServiceTypeKind);
        return IsServiceTypeSupported(serviceType);
    }

    private static bool IsServiceTypeSupported(AIServiceType serviceType)
    {
        return serviceType is AIServiceType.OpenAI or AIServiceType.AzureOpenAI;
    }

    private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
    {
        return serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
    }

    private static bool RequiresApiKey(AIServiceType serviceType)
    {
        return true;
    }

    private static string RequireEndpoint(string endpoint, AIServiceType serviceType)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        throw new InvalidOperationException($"Endpoint is required for {serviceType} configuration but was not provided.");
    }

    private PromptExecutionSettings CreatePromptExecutionSettings()
    {
        var serviceType = GetRuntimeConfiguration().ServiceType;
        return new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
    }
}
