// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services;

internal static class AdvancedAIProviderResolver
{
    public static bool TryResolveAdvancedProvider(PasteAIConfiguration configuration, string providerIdOverride, out PasteAIProviderDefinition provider)
    {
        provider = null;

        if (configuration is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(providerIdOverride))
        {
            var configuredProvider = configuration.Providers?.FirstOrDefault(candidate => string.Equals(candidate.Id, providerIdOverride, StringComparison.OrdinalIgnoreCase));
            if (configuredProvider is not null)
            {
                if (!IsAdvancedProvider(configuredProvider))
                {
                    return false;
                }

                provider = configuredProvider;
                return true;
            }
        }

        var activeProvider = configuration.ActiveProvider;
        if (IsAdvancedProvider(activeProvider))
        {
            provider = activeProvider;
            return true;
        }

        if (activeProvider is not null)
        {
            return false;
        }

        provider = configuration.Providers?.FirstOrDefault(IsAdvancedProvider);
        return provider is not null;
    }

    private static bool IsAdvancedProvider(PasteAIProviderDefinition provider)
    {
        if (provider is null || !provider.EnableAdvancedAI)
        {
            return false;
        }

        var serviceType = provider.ServiceTypeKind == AIServiceType.Unknown ? AIServiceType.OpenAI : provider.ServiceTypeKind;
        return serviceType is AIServiceType.OpenAI or AIServiceType.AzureOpenAI;
    }
}
