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
using Microsoft.SemanticKernel.Connectors.Amazon;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services;

public sealed class AdvancedAIKernelService : KernelServiceBase
{
    private readonly IAICredentialsProvider credentialsProvider;

    private readonly record struct RuntimeConfig(
        AIServiceType ServiceType,
        string ModelName,
        string Endpoint,
        string DeploymentName,
        string ModelPath,
        bool UsePasteScope);

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

    protected override string AdvancedAIModelName => GetRuntimeConfig().ModelName;

    protected override PromptExecutionSettings PromptExecutionSettings => CreatePromptExecutionSettings();

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);

        var runtimeConfig = GetRuntimeConfig();
        var serviceType = runtimeConfig.ServiceType;
        var modelName = runtimeConfig.ModelName;
        var requiresApiKey = RequiresApiKey(serviceType);
        var apiKey = string.Empty;
        if (requiresApiKey)
        {
            var scope = runtimeConfig.UsePasteScope ? AICredentialScope.PasteAI : AICredentialScope.AdvancedAI;
            this.credentialsProvider.Refresh(scope);
            apiKey = (this.credentialsProvider.GetKey(scope) ?? string.Empty).Trim();
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
            case AIServiceType.Mistral:
                kernelBuilder.AddMistralChatCompletion(modelName, apiKey: apiKey);
                break;
            case AIServiceType.Google:
                kernelBuilder.AddGoogleAIGeminiChatCompletion(modelName, apiKey: apiKey);
                break;
            case AIServiceType.HuggingFace:
                kernelBuilder.AddHuggingFaceChatCompletion(modelName, apiKey: apiKey);
                break;
            case AIServiceType.AzureAIInference:
                kernelBuilder.AddAzureAIInferenceChatCompletion(modelName, apiKey: apiKey);
                break;
            case AIServiceType.Ollama:
                kernelBuilder.AddOllamaChatCompletion(modelName, endpoint: new Uri(RequireEndpoint(endpoint, serviceType)));
                break;
            case AIServiceType.Anthropic:
                kernelBuilder.AddBedrockChatCompletionService(modelName);
                break;
            case AIServiceType.AmazonBedrock:
                kernelBuilder.AddBedrockChatCompletionService(modelName);
                break;
            default:
                throw new NotSupportedException($"Service type '{runtimeConfig.ServiceType}' is not supported");
        }
    }

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        return AIServiceUsageHelper.GetOpenAIServiceUsage(chatMessage);
    }

    private PasteAIProviderDefinition GetConfiguration()
    {
        var config = this.UserSettings?.PasteAIConfiguration.Providers.FirstOrDefault(
            p => p.EnableAdvancedAI);
        if (config is null)
        {
            return new PasteAIProviderDefinition();
        }

        return config;
    }

    private static string GetModelName(AdvancedAIConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config?.ModelName))
        {
            return config.ModelName;
        }

        return "gpt-4o";
    }

    private RuntimeConfig GetRuntimeConfig()
    {
        if (TryGetActiveProviderConfig(out var providerConfig))
        {
            return providerConfig;
        }

        var fallback = GetConfiguration();
        var serviceType = NormalizeServiceType(fallback.ServiceTypeKind);
        return new RuntimeConfig(
            serviceType,
            GetModelName(fallback),
            fallback.EndpointUrl,
            fallback.DeploymentName,
            fallback.ModelPath,
            UsePasteScope: false);
    }

    private bool TryGetActiveProviderConfig(out RuntimeConfig runtimeConfig)
    {
        runtimeConfig = default;
        var provider = this.UserSettings?.PasteAIConfiguration?.ActiveProvider;
        if (provider is null)
        {
            return false;
        }

        var serviceType = NormalizeServiceType(provider.ServiceTypeKind);
        if (!IsServiceTypeSupported(serviceType))
        {
            return false;
        }

        var fallback = GetConfiguration();
        var modelName = !string.IsNullOrWhiteSpace(provider.ModelName) ? provider.ModelName : GetModelName(fallback);

        runtimeConfig = new RuntimeConfig(
            serviceType,
            modelName,
            provider.EndpointUrl,
            provider.DeploymentName,
            provider.ModelPath,
            UsePasteScope: true);
        return true;
    }

    private static bool IsServiceTypeSupported(AIServiceType serviceType)
    {
        return serviceType switch
        {
            AIServiceType.OpenAI
            or AIServiceType.AzureOpenAI
            or AIServiceType.Mistral
            or AIServiceType.Google
            or AIServiceType.HuggingFace
            or AIServiceType.AzureAIInference
            or AIServiceType.Ollama
            or AIServiceType.Anthropic
            or AIServiceType.AmazonBedrock => true,
            _ => false,
        };
    }

    private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
    {
        return serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
    }

    private static bool RequiresApiKey(AIServiceType serviceType)
    {
        return serviceType switch
        {
            AIServiceType.Ollama => false,
            AIServiceType.Anthropic => false,
            AIServiceType.AmazonBedrock => false,
            _ => true,
        };
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
        var serviceType = GetRuntimeConfig().ServiceType;
        return serviceType switch
        {
            AIServiceType.OpenAI or AIServiceType.AzureOpenAI => new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.01,
            },
            _ => new PromptExecutionSettings(),
        };
    }
}
