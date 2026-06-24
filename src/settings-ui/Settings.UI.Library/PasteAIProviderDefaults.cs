// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Provides default values for Paste AI provider definitions.
    /// </summary>
    public static class PasteAIProviderDefaults
    {
        /// <summary>
        /// Gets the default model name for a given AI service type.
        /// </summary>
        public static string GetDefaultModelName(AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "gpt-4o",
                AIServiceType.AzureOpenAI => "gpt-4o",
                AIServiceType.Mistral => "mistral-large-latest",
                AIServiceType.Google => "gemini-1.5-pro",
                AIServiceType.AzureAIInference => "gpt-4o-mini",
                AIServiceType.Ollama => "llama3",
                _ => string.Empty,
            };
        }
    }
}
