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

            if (IsSupportedBySemanticKernel(config.ProviderType))
            {
                return new SemanticKernelPasteProvider(config);
            }

            return config.ProviderType switch
            {
                "local" => new LocalModelPasteProvider(config.LocalModelPath),
                _ => throw new NotSupportedException($"Provider {config.ProviderType} not supported"),
            };
        }

        private static bool IsSupportedBySemanticKernel(string providerType)
        {
            return providerType switch
            {
                "openai" or "azure" => true,
                _ => false,
            };
        }
    }
}
