// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class PasteAIProviderFactory : IPasteAIProviderFactory
    {
        public IPasteAIProvider CreateProvider(PasteAIConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var providerType = NormalizeProviderType(config.ProviderType);
            config.ProviderType = providerType;

            if (IsSupportedBySemanticKernel(providerType))
            {
                return new SemanticKernelPasteProvider(config);
            }

            return providerType switch
            {
                "local" or "onnx" => new LocalModelPasteProvider(config.LocalModelPath ?? config.ModelPath),
                _ => throw new NotSupportedException($"Provider {config.ProviderType} not supported"),
            };
        }

        private static bool IsSupportedBySemanticKernel(string providerType)
        {
            return providerType switch
            {
                "openai" or "azureopenai" => true,
                _ => false,
            };
        }

        private static string NormalizeProviderType(string providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
            {
                return "openai";
            }

            var normalized = providerType.Trim().ToLowerInvariant();

            return normalized switch
            {
                "azure" => "azureopenai",
                _ => normalized,
            };
        }
    }
}
