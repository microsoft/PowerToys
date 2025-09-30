// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services;

/// <summary>
/// Factory for creating AI service instances based on configuration
/// </summary>
public static class AIServiceFactory
{
    /// <summary>
    /// Create a kernel service for Advanced AI features
    /// </summary>
    public static IKernelService CreateAdvancedAIService(
        AdvancedAIConfiguration config,
        IAICredentialsProvider credentialsProvider,
        IKernelQueryCacheService queryCacheService,
        IPromptModerationService promptModerationService,
        IUserSettings userSettings,
        ICustomActionTransformService customActionTransformService)
    {
        var provider = credentialsProvider ?? CreateCredentialsProvider(config);

        return config.ServiceType.ToLowerInvariant() switch
        {
            "openai" => new ConfigurableKernelService(
                config,
                provider,
                queryCacheService,
                promptModerationService,
                userSettings,
                customActionTransformService),
            "azureopenai" => new ConfigurableKernelService(
                config,
                provider,
                queryCacheService,
                promptModerationService,
                userSettings,
                customActionTransformService),
            _ => throw new NotSupportedException($"AI service '{config.ServiceType}' is not supported for Advanced AI"),
        };
    }

    /// <summary>
    /// Create a kernel service for Paste AI features
    /// </summary>
    public static IKernelService CreatePasteAIService(
        PasteAIConfiguration config,
        IAICredentialsProvider credentialsProvider,
        IKernelQueryCacheService queryCacheService,
        IPromptModerationService promptModerationService,
        IUserSettings userSettings,
        ICustomActionTransformService customActionTransformService)
    {
        var provider = credentialsProvider ?? CreateCredentialsProvider(config);

        return config.ServiceType.ToLowerInvariant() switch
        {
            "openai" => new ConfigurableKernelService(
                config,
                provider,
                queryCacheService,
                promptModerationService,
                userSettings,
                customActionTransformService),
            "azureopenai" => new ConfigurableKernelService(
                config,
                provider,
                queryCacheService,
                promptModerationService,
                userSettings,
                customActionTransformService),
            _ => throw new NotSupportedException($"AI service '{config.ServiceType}' is not supported for Paste AI"),
        };
    }

    private static IAICredentialsProvider CreateCredentialsProvider(AdvancedAIConfiguration config)
    {
        if (config.UseSharedCredentials && (config.ServiceType.ToLowerInvariant() is "openai" or "azureopenai"))
        {
            // Use the original credential key for backward compatibility
            return new OpenAI.VaultCredentialsProvider();
        }

        return new EnhancedVaultCredentialsProvider(config.ServiceType, "AdvancedAI");
    }

    private static IAICredentialsProvider CreateCredentialsProvider(PasteAIConfiguration config)
    {
        if (config.UseSharedCredentials && (config.ServiceType.ToLowerInvariant() is "openai" or "azureopenai"))
        {
            // Use the original credential key for backward compatibility
            return new OpenAI.VaultCredentialsProvider();
        }

        return new EnhancedVaultCredentialsProvider(config.ServiceType, "PasteAI");
    }
}
