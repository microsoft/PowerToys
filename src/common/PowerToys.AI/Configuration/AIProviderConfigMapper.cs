// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.AI.Credentials;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI.Configuration;

/// <summary>
/// Helpers for mapping persisted AI provider definitions to runtime <see cref="AIProviderConfig"/>.
/// </summary>
public static class AIProviderConfigMapper
{
    public static AIProviderConfig FromDefinition(
        AIProviderDefinition definition,
        IAICredentialsProvider? credentialsProvider = null,
        string? apiKeyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var serviceType = definition.ServiceTypeKind;
        var apiKey = apiKeyOverride;
        if (string.IsNullOrEmpty(apiKey) && credentialsProvider is not null && AICredentialVault.RequiresApiKey(serviceType))
        {
            apiKey = credentialsProvider.GetKey(serviceType, definition.Id);
        }

        return new AIProviderConfig
        {
            ProviderType = serviceType,
            Model = definition.ModelName ?? string.Empty,
            ApiKey = apiKey ?? string.Empty,
            Endpoint = definition.EndpointUrl ?? string.Empty,
            DeploymentName = definition.DeploymentName ?? string.Empty,
            ModelPath = definition.ModelPath ?? string.Empty,
            SystemPrompt = definition.SystemPrompt ?? string.Empty,
            ModerationEnabled = definition.ModerationEnabled,
        };
    }

    public static AIProviderConfig FromConfiguration(
        AIConfiguration? configuration,
        IAICredentialsProvider? credentialsProvider = null,
        string? providerIdOverride = null)
    {
        var provider = ResolveProvider(configuration, providerIdOverride);
        if (provider is null)
        {
            return new AIProviderConfig { ProviderType = AIServiceType.OpenAI };
        }

        return FromDefinition(provider, credentialsProvider);
    }

    public static AIProviderDefinition? ResolveProvider(AIConfiguration? configuration, string? providerIdOverride = null)
    {
        if (configuration?.Providers is null || configuration.Providers.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(providerIdOverride))
        {
            var match = configuration.Providers.FirstOrDefault(
                p => string.Equals(p.Id, providerIdOverride, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return configuration.ActiveProvider;
    }

    public static bool IsLocalProvider(AIServiceType serviceType) =>
        serviceType is AIServiceType.FoundryLocal
            or AIServiceType.PhiSilica
            or AIServiceType.Ollama
            or AIServiceType.Onnx
            or AIServiceType.ML;
}
