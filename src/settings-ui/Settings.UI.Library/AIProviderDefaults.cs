// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Default model names for AI providers.
    /// </summary>
    public static class AIProviderDefaults
    {
        public static string GetDefaultModelName(AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI or AIServiceType.AzureOpenAI => "gpt-4o",
                AIServiceType.Mistral => "mistral-large-latest",
                AIServiceType.Google => "gemini-1.5-pro",
                AIServiceType.AzureAIInference => "gpt-4o-mini",
                AIServiceType.Ollama => "llama3",
                _ => string.Empty,
            };
        }
    }
}
