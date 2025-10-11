// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class PasteAIProviderFactory : IPasteAIProviderFactory
    {
        public IPasteAIProvider CreateProvider(PasteAIConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var serviceType = config.ProviderType;
            if (serviceType == AIServiceType.Unknown)
            {
                serviceType = AIServiceType.OpenAI;
                config.ProviderType = serviceType;
            }

            if (IsSupportedBySemanticKernel(serviceType))
            {
                return new SemanticKernelPasteProvider(config);
            }

            return serviceType switch
            {
                AIServiceType.ML => new LocalModelPasteProvider(config.LocalModelPath ?? config.ModelPath),
                AIServiceType.FoundryLocal => new FoundryLocalPasteProvider(config),
                _ => throw new NotSupportedException($"Provider {config.ProviderType} not supported"),
            };
        }

        private static readonly HashSet<AIServiceType> SupportedProviders =
        [
            AIServiceType.OpenAI,
            AIServiceType.AzureOpenAI,
            AIServiceType.Onnx,
        ];

        private static bool IsSupportedBySemanticKernel(AIServiceType providerType)
            => SupportedProviders.Contains(providerType);
    }
}
