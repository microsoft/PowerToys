// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services;

/// <summary>
/// Configurable kernel service that can work with different AI service configurations
/// </summary>
public sealed class ConfigurableKernelService : KernelServiceBase
{
    private readonly object _config;
    private readonly IAICredentialsProvider _aiCredentialsProvider;

    public ConfigurableKernelService(
        AdvancedAIConfiguration config,
        IAICredentialsProvider aiCredentialsProvider,
        IKernelQueryCacheService queryCacheService,
        IPromptModerationService promptModerationService,
        IUserSettings userSettings,
        ICustomActionTransformService customActionTransformService)
        : base(queryCacheService, promptModerationService, userSettings, customActionTransformService)
    {
        _config = config;
        _aiCredentialsProvider = aiCredentialsProvider;
    }

    public ConfigurableKernelService(
        PasteAIConfiguration config,
        IAICredentialsProvider aiCredentialsProvider,
        IKernelQueryCacheService queryCacheService,
        IPromptModerationService promptModerationService,
        IUserSettings userSettings,
        ICustomActionTransformService customActionTransformService)
        : base(queryCacheService, promptModerationService, userSettings, customActionTransformService)
    {
        _config = config;
        _aiCredentialsProvider = aiCredentialsProvider;
    }

    protected override string AdvancedAIModelName => GetModelName();

    protected override PromptExecutionSettings PromptExecutionSettings =>
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.01,
        };

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder)
    {
        var serviceType = GetServiceType();
        var modelName = GetModelName();

        switch (serviceType.ToLowerInvariant())
        {
            case "openai":
                kernelBuilder.AddOpenAIChatCompletion(modelName, _aiCredentialsProvider.Key, serviceId: modelName);
                break;
            case "azureopenai":
                var endpoint = GetEndpointUrl();
                var deploymentName = GetDeploymentName() ?? modelName;
                kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, _aiCredentialsProvider.Key, serviceId: modelName);
                break;
            default:
                throw new System.NotSupportedException($"Service type '{serviceType}' is not supported");
        }
    }

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        return AIServiceUsageHelper.GetOpenAIServiceUsage(chatMessage);
    }

    private string GetServiceType()
    {
        return _config switch
        {
            AdvancedAIConfiguration advancedConfig => advancedConfig.ServiceType,
            PasteAIConfiguration pasteConfig => pasteConfig.ServiceType,
            _ => "OpenAI",
        };
    }

    private string GetModelName()
    {
        return _config switch
        {
            AdvancedAIConfiguration advancedConfig => advancedConfig.ModelName,
            PasteAIConfiguration pasteConfig => pasteConfig.ModelName,
            _ => "gpt-4o",
        };
    }

    private string GetEndpointUrl()
    {
        return _config switch
        {
            AdvancedAIConfiguration advancedConfig => advancedConfig.EndpointUrl,
            PasteAIConfiguration pasteConfig => pasteConfig.EndpointUrl,
            _ => null,
        };
    }

    private string GetDeploymentName()
    {
        return _config switch
        {
            AdvancedAIConfiguration advancedConfig => advancedConfig.DeploymentName,
            PasteAIConfiguration pasteConfig => pasteConfig.DeploymentName,
            _ => null,
        };
    }
}
