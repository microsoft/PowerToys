// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.OpenAI;

public sealed class KernelService(IKernelQueryCacheService queryCacheService, IAICredentialsProvider aiCredentialsProvider, IPromptModerationService promptModerationService, IUserSettings userSettings) :
    KernelServiceBase(queryCacheService, promptModerationService, userSettings)
{
    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;

    protected override string ModelName => "gpt-4o";

    protected override PromptExecutionSettings PromptExecutionSettings =>
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.01,
        };

    protected override PromptExecutionSettings CustomTextTransformExecutionSettings =>
        new OpenAIPromptExecutionSettings
        {
            Temperature = 0.01,
            MaxTokens = 2000,
            FunctionChoiceBehavior = null,
        };

    protected override void AddChatCompletionService(IKernelBuilder kernelBuilder) => kernelBuilder.AddOpenAIChatCompletion(ModelName, _aiCredentialsProvider.Key);

    protected override AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage)
    {
        // Try to get usage information from metadata
        if (chatMessage.Metadata?.TryGetValue("Usage", out var usageObj) == true)
        {
            // Handle different possible usage types through reflection to be version-agnostic
            var usageType = usageObj.GetType();

            try
            {
                // Try common property names for prompt tokens
                var promptTokensProp = usageType.GetProperty("PromptTokens") ?? usageType.GetProperty("InputTokens") ?? usageType.GetProperty("InputTokenCount");
                var completionTokensProp = usageType.GetProperty("CompletionTokens") ?? usageType.GetProperty("OutputTokens") ?? usageType.GetProperty("OutputTokenCount");

                if (promptTokensProp != null && completionTokensProp != null)
                {
                    var promptTokens = (int)(promptTokensProp.GetValue(usageObj) ?? 0);
                    var completionTokens = (int)(completionTokensProp.GetValue(usageObj) ?? 0);
                    return new AIServiceUsage(promptTokens, completionTokens);
                }
            }
            catch
            {
                // If reflection fails, fall back to no usage
            }
        }

        return AIServiceUsage.None;
    }
}
