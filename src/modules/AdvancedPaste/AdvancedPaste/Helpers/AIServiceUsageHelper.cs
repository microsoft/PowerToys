// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;
using Microsoft.SemanticKernel;

namespace AdvancedPaste.Helpers;

/// <summary>
/// Helper class for extracting AI service usage information from chat messages.
/// </summary>
public static class AIServiceUsageHelper
{
    /// <summary>
    /// Extracts AI service usage information from OpenAI chat message metadata.
    /// </summary>
    /// <param name="chatMessage">The chat message containing usage metadata.</param>
    /// <returns>AI service usage information or AIServiceUsage.None if extraction fails.</returns>
    public static AIServiceUsage GetOpenAIServiceUsage(ChatMessageContent chatMessage)
    {
        // Try to get usage information from metadata
        if (chatMessage.Metadata?.TryGetValue("Usage", out var usageObj) == true)
        {
            // Handle different possible usage types through reflection to be version-agnostic
            var usageType = usageObj.GetType();

            try
            {
                // Try common property names for prompt tokens
                var promptTokensProp = usageType.GetProperty("PromptTokens") ??
                                     usageType.GetProperty("InputTokens") ??
                                     usageType.GetProperty("InputTokenCount");

                var completionTokensProp = usageType.GetProperty("CompletionTokens") ??
                                         usageType.GetProperty("OutputTokens") ??
                                         usageType.GetProperty("OutputTokenCount");

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
