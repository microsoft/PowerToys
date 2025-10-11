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

    protected override PromptExecutionSettings PromptExecutionSettings =>
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.01,
        };

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);

        var config = this.GetConfiguration();
        var serviceType = config.ServiceTypeKind;
        var modelName = GetModelName(config);
        var apiKey = this.credentialsProvider.GetKey(AICredentialScope.AdvancedAI);

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                kernelBuilder.AddOpenAIChatCompletion(modelName, apiKey, serviceId: modelName);
                break;
            case AIServiceType.AzureOpenAI:
                var endpoint = config.EndpointUrl;
                var deploymentName = string.IsNullOrWhiteSpace(config.DeploymentName) ? modelName : config.DeploymentName;
                kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey, serviceId: modelName);
                break;
            default:
                throw new NotSupportedException($"Service type '{config.ServiceType}' is not supported");
        }
    }

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        return AIServiceUsageHelper.GetOpenAIServiceUsage(chatMessage);
    }

    private AdvancedAIConfiguration GetConfiguration()
    {
        var config = this.UserSettings?.AdvancedAIConfiguration;
        if (config is null)
        {
            return new AdvancedAIConfiguration();
        }

        return config;
    }

    private string GetModelName()
    {
        return GetModelName(this.GetConfiguration());
    }

    private static string GetModelName(AdvancedAIConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config?.ModelName))
        {
            return config.ModelName;
        }

        return "gpt-4o";
    }
}
