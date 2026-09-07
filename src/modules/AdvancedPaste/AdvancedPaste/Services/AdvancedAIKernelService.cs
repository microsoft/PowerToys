// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services;

public sealed class AdvancedAIKernelService : KernelServiceBase
{
    private sealed record RuntimeConfiguration(
        string ProviderId,
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

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder, IKernelRuntimeConfiguration runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        var serviceType = runtimeConfig.ServiceType;
        var modelName = runtimeConfig.ModelName;
        var requiresApiKey = RequiresApiKey(serviceType);
        var apiKey = string.Empty;
        if (requiresApiKey)
        {
            apiKey = (this.credentialsProvider.GetKey(serviceType, runtimeConfig.ProviderId) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"An API key is required for {serviceType} but none was found in the credential vault.");
            }
        }

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

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        return AIServiceUsageHelper.GetOpenAIServiceUsage(chatMessage);
    }

    protected override bool ShouldModerateAdvancedAI(IKernelRuntimeConfiguration runtimeConfig)
    {
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

    protected override IKernelRuntimeConfiguration GetRuntimeConfiguration(string providerIdOverride)
    {
        if (TryGetRuntimeConfiguration(providerIdOverride, out var runtimeConfig))
        {
            return runtimeConfig;
        }

        throw new InvalidOperationException("No Advanced AI provider is configured.");
    }

    private bool TryGetRuntimeConfiguration(string providerIdOverride, out IKernelRuntimeConfiguration runtimeConfig)
    {
        runtimeConfig = null;

        if (!AdvancedAIProviderResolver.TryResolveAdvancedProvider(this.UserSettings?.PasteAIConfiguration, providerIdOverride, out var provider))
        {
            return false;
        }

        var serviceType = NormalizeServiceType(provider.ServiceTypeKind);
        if (!IsServiceTypeSupported(serviceType))
        {
            return false;
        }

        runtimeConfig = new RuntimeConfiguration(
            provider.Id,
            serviceType,
            GetModelName(provider),
            provider.EndpointUrl,
            provider.DeploymentName,
            provider.ModelPath,
            provider.SystemPrompt,
            provider.ModerationEnabled);
        return true;
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

    protected override PromptExecutionSettings GetPromptExecutionSettings(IKernelRuntimeConfiguration runtimeConfig)
    {
        return new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
    }
}
