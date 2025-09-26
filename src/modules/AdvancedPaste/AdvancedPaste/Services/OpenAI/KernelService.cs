// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using AdvancedPaste.Models;
using AdvancedPaste.Settings;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ChatTokenUsage = OpenAI.Chat.ChatTokenUsage;

namespace AdvancedPaste.Services.OpenAI;

public sealed class KernelService(IUserSettings userSettings, IKernelQueryCacheService queryCacheService, IAICredentialsProvider aiCredentialsProvider, IPromptModerationService promptModerationService, ICustomTextTransformService customTextTransformService) :
    KernelServiceBase(queryCacheService, promptModerationService, customTextTransformService)
{
    private readonly IUserSettings _userSettings = userSettings;
    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;

    protected override string ModelName => string.IsNullOrEmpty(_userSettings.CustomModelName) ? "gpt-4o" : _userSettings.CustomModelName;

    protected override PromptExecutionSettings PromptExecutionSettings =>
        new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.01,
        };

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder)
    {
        if (string.IsNullOrEmpty(_userSettings.CustomEndpoint))
        {
            kernelBuilder.AddOpenAIChatCompletion(ModelName, _aiCredentialsProvider.Key);
        }
        else
        {
            kernelBuilder.AddOpenAIChatCompletion(ModelName, new Uri(_userSettings.CustomEndpoint), _aiCredentialsProvider.Key);
        }
    }

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage) =>
        chatMessage.Metadata?.GetValueOrDefault("Usage") is ChatTokenUsage completionsUsage
            ? new(PromptTokens: completionsUsage.InputTokenCount, CompletionTokens: completionsUsage.OutputTokenCount)
            : AIServiceUsage.None;
}
