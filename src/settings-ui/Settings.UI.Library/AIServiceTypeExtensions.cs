// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class AIServiceTypeExtensions
    {
        /// <summary>
        /// Convert a persisted string value into an <see cref="AIServiceType"/>.
        /// Supports historical casing and aliases.
        /// </summary>
        public static AIServiceType ToAIServiceType(this string serviceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                return AIServiceType.OpenAI;
            }

            var normalized = serviceType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "openai" => AIServiceType.OpenAI,
                "azureopenai" or "azure" => AIServiceType.AzureOpenAI,
                "onnx" => AIServiceType.Onnx,
                "foundrylocal" or "foundry" or "fl" => AIServiceType.FoundryLocal,
                "ml" or "windowsml" or "winml" => AIServiceType.ML,
                "mistral" => AIServiceType.Mistral,
                "google" or "googleai" or "googlegemini" => AIServiceType.Google,
                "huggingface" => AIServiceType.HuggingFace,
                "azureaiinference" or "azureinference" => AIServiceType.AzureAIInference,
                "ollama" => AIServiceType.Ollama,
                "anthropic" => AIServiceType.Anthropic,
                "amazonbedrock" or "bedrock" => AIServiceType.AmazonBedrock,
                _ => AIServiceType.Unknown,
            };
        }

        /// <summary>
        /// Convert an <see cref="AIServiceType"/> to the canonical string used for persistence.
        /// </summary>
        public static string ToConfigurationString(this AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "OpenAI",
                AIServiceType.AzureOpenAI => "AzureOpenAI",
                AIServiceType.Onnx => "Onnx",
                AIServiceType.FoundryLocal => "FoundryLocal",
                AIServiceType.ML => "ML",
                AIServiceType.Mistral => "Mistral",
                AIServiceType.Google => "Google",
                AIServiceType.HuggingFace => "HuggingFace",
                AIServiceType.AzureAIInference => "AzureAIInference",
                AIServiceType.Ollama => "Ollama",
                AIServiceType.Anthropic => "Anthropic",
                AIServiceType.AmazonBedrock => "AmazonBedrock",
                AIServiceType.Unknown => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(serviceType), serviceType, "Unsupported AI service type."),
            };
        }

        /// <summary>
        /// Convert an <see cref="AIServiceType"/> into the normalized key used internally.
        /// </summary>
        public static string ToNormalizedKey(this AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "openai",
                AIServiceType.AzureOpenAI => "azureopenai",
                AIServiceType.Onnx => "onnx",
                AIServiceType.FoundryLocal => "foundrylocal",
                AIServiceType.ML => "ml",
                AIServiceType.Mistral => "mistral",
                AIServiceType.Google => "google",
                AIServiceType.HuggingFace => "huggingface",
                AIServiceType.AzureAIInference => "azureaiinference",
                AIServiceType.Ollama => "ollama",
                AIServiceType.Anthropic => "anthropic",
                AIServiceType.AmazonBedrock => "amazonbedrock",
                _ => string.Empty,
            };
        }
    }
}
