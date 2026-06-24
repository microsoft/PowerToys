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
        private static readonly IReadOnlyList<PasteAIProviderRegistration> ProviderRegistrations = new[]
        {
            SemanticKernelPasteProvider.Registration,
            LocalModelPasteProvider.Registration,
            FoundryLocalPasteProvider.Registration,
        };

        private static readonly IReadOnlyDictionary<AIServiceType, Func<PasteAIConfig, IPasteAIProvider>> ProviderFactories = CreateProviderFactories();

        public IPasteAIProvider CreateProvider(PasteAIConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var serviceType = config.ProviderType;
            if (serviceType == AIServiceType.Unknown)
            {
                serviceType = AIServiceType.OpenAI;
                config.ProviderType = serviceType;
            }

            if (!ProviderFactories.TryGetValue(serviceType, out var factory))
            {
                throw new NotSupportedException($"Provider {config.ProviderType} not supported");
            }

            return factory(config);
        }

        private static IReadOnlyDictionary<AIServiceType, Func<PasteAIConfig, IPasteAIProvider>> CreateProviderFactories()
        {
            var map = new Dictionary<AIServiceType, Func<PasteAIConfig, IPasteAIProvider>>();

            foreach (var registration in ProviderRegistrations)
            {
                Register(map, registration.SupportedTypes, registration.Factory);
            }

            return map;
        }

        private static void Register(Dictionary<AIServiceType, Func<PasteAIConfig, IPasteAIProvider>> map, IReadOnlyCollection<AIServiceType> types, Func<PasteAIConfig, IPasteAIProvider> factory)
        {
            foreach (var type in types)
            {
                map[type] = factory;
            }
        }
    }
}
