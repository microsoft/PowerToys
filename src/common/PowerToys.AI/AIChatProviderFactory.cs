// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Default factory that registers Semantic Kernel, Foundry Local, Phi Silica, and local-model stubs.
/// </summary>
public sealed class AIChatProviderFactory : IAIChatProviderFactory
{
    private static readonly IReadOnlyList<AIChatProviderRegistration> ProviderRegistrations =
    [
        SemanticKernelChatProvider.Registration,
        FoundryLocalChatProvider.Registration,
        PhiSilicaChatProvider.Registration,
        LocalModelChatProvider.Registration,
    ];

    private static readonly IReadOnlyDictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>> ProviderFactories =
        CreateProviderFactories();

    public IAIChatProvider CreateProvider(AIProviderConfig config)
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
            throw new NotSupportedException($"Provider {config.ProviderType} is not supported.");
        }

        return factory(config);
    }

    private static IReadOnlyDictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>> CreateProviderFactories()
    {
        var map = new Dictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>>();

        foreach (var registration in ProviderRegistrations)
        {
            foreach (var type in registration.SupportedTypes)
            {
                map[type] = registration.Factory;
            }
        }

        return map;
    }
}
