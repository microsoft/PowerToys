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

    protected override string AdvancedAIModelName => GetModelName();

    protected override PromptExecutionSettings PromptExecutionSettings => CreatePromptExecutionSettings();

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);

        var config = this.GetConfiguration();
        var serviceType = config.ServiceTypeKind;
        var modelName = GetModelName(config);
        var requiresApiKey = RequiresApiKey(serviceType);
        var apiKey = string.Empty;
        if (requiresApiKey)
        {
            this.credentialsProvider.Refresh(AICredentialScope.AdvancedAI);
            apiKey = (this.credentialsProvider.GetKey(AICredentialScope.AdvancedAI) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"An API key is required for {serviceType} but none was found in the credential vault.");
            }
        }

        var endpoint = string.IsNullOrWhiteSpace(config.EndpointUrl) ? null : config.EndpointUrl.Trim();

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                kernelBuilder.AddOpenAIChatCompletion(modelName, apiKey, serviceId: modelName);
                break;
            case AIServiceType.AzureOpenAI:
                var deploymentName = string.IsNullOrWhiteSpace(config.DeploymentName) ? modelName : config.DeploymentName;
                kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, RequireEndpoint(endpoint, serviceType), apiKey, serviceId: modelName);
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
                kernelBuilder.AddOllamaChatCompletion(modelName, endpoint: new Uri(endpoint));
                break;
            case AIServiceType.Anthropic:
                kernelBuilder.AddBedrockChatCompletionService(modelName);
                break;
            case AIServiceType.AmazonBedrock:
                kernelBuilder.AddBedrockChatCompletionService(modelName);
                break;
            default:
                throw new NotSupportedException($"Service type '{config.ServiceType}' is not supported");
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

    private string GetModelName()
    {
        return GetModelName(this.GetConfiguration());
    }

    private static string GetModelName(PasteAIProviderDefinition config)
    {
        if (!string.IsNullOrWhiteSpace(config?.ModelName))
        {
            return config.ModelName;
        }

        return "gpt-4o";
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
        var serviceType = GetConfiguration().ServiceTypeKind;
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
