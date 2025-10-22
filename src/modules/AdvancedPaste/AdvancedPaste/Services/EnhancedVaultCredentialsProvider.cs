// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.Security.Credentials;

namespace AdvancedPaste.Services;

/// <summary>
/// Enhanced credentials provider that supports different AI service types
/// Keys are stored in Windows Credential Vault with service-specific identifiers
/// </summary>
public sealed class EnhancedVaultCredentialsProvider : IAICredentialsProvider
{
    private sealed class CredentialSlot
    {
        public AIServiceType ServiceType { get; set; } = AIServiceType.Unknown;

        public string ProviderId { get; set; } = string.Empty;

        public (string Resource, string Username)? Entry { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    private readonly IUserSettings _userSettings;
    private readonly Dictionary<AICredentialScope, CredentialSlot> _slots;
    private readonly object _syncRoot = new();

    public EnhancedVaultCredentialsProvider(IUserSettings userSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));

        _slots = new Dictionary<AICredentialScope, CredentialSlot>
        {
            [AICredentialScope.PasteAI] = new CredentialSlot(),
            [AICredentialScope.AdvancedAI] = new CredentialSlot(),
        };

        Refresh(AICredentialScope.PasteAI);
        Refresh(AICredentialScope.AdvancedAI);
    }

    public string GetKey(AICredentialScope scope)
    {
        lock (_syncRoot)
        {
            UpdateSlot(scope, forceRefresh: false);
            return _slots[scope].Key;
        }
    }

    public bool IsConfigured(AICredentialScope scope)
    {
        return !string.IsNullOrEmpty(GetKey(scope));
    }

    public bool Refresh(AICredentialScope scope)
    {
        lock (_syncRoot)
        {
            return UpdateSlot(scope, forceRefresh: true);
        }
    }

    private bool UpdateSlot(AICredentialScope scope, bool forceRefresh)
    {
        var slot = _slots[scope];
        var (serviceType, providerId) = ResolveCredentialTarget(scope);
        var desiredServiceType = NormalizeServiceType(serviceType);
        providerId ??= string.Empty;

        var hasChanged = false;

        if (slot.ServiceType != desiredServiceType || !string.Equals(slot.ProviderId, providerId, StringComparison.Ordinal))
        {
            slot.ServiceType = desiredServiceType;
            slot.ProviderId = providerId;
            slot.Entry = BuildCredentialEntry(desiredServiceType, providerId, scope);
            forceRefresh = true;
            hasChanged = true;
        }

        if (!forceRefresh)
        {
            return hasChanged;
        }

        var newKey = LoadKey(slot.Entry);
        if (!string.Equals(slot.Key, newKey, StringComparison.Ordinal))
        {
            slot.Key = newKey;
            hasChanged = true;
        }

        return hasChanged;
    }

    private (AIServiceType ServiceType, string ProviderId) ResolveCredentialTarget(AICredentialScope scope)
    {
        return scope switch
        {
            AICredentialScope.AdvancedAI => (ResolveAdvancedAiServiceType(), string.Empty),
            AICredentialScope.PasteAI => ResolvePasteAiServiceTarget(),
            _ => (AIServiceType.OpenAI, string.Empty),
        };
    }

    private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
    {
        return serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
    }

    private AIServiceType ResolveAdvancedAiServiceType()
    {
        return _userSettings.AdvancedAIConfiguration?.ServiceTypeKind ?? AIServiceType.OpenAI;
    }

    private (AIServiceType ServiceType, string ProviderId) ResolvePasteAiServiceTarget()
    {
        var provider = _userSettings.PasteAIConfiguration?.ActiveProvider;
        if (provider is null)
        {
            return (AIServiceType.OpenAI, string.Empty);
        }

        return (provider.ServiceTypeKind, provider.Id ?? string.Empty);
    }

    private static string LoadKey((string Resource, string Username)? entry)
    {
        if (entry is null)
        {
            return string.Empty;
        }

        try
        {
            var credential = new PasswordVault().Retrieve(entry.Value.Resource, entry.Value.Username);
            return credential?.Password ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static (string Resource, string Username)? BuildCredentialEntry(AIServiceType serviceType, string providerId, AICredentialScope scope)
    {
        return scope switch
        {
            AICredentialScope.AdvancedAI => GetAdvancedAiEntry(serviceType),
            AICredentialScope.PasteAI => GetPasteAiEntry(serviceType, providerId),
            _ => null,
        };
    }

    private static (string Resource, string Username)? GetAdvancedAiEntry(AIServiceType serviceType)
    {
        return serviceType switch
        {
            AIServiceType.OpenAI => ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_AdvancedAI_OpenAI"),
            AIServiceType.AzureOpenAI => ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_AdvancedAI_AzureOpenAI"),
            AIServiceType.AzureAIInference => ("https://azure.microsoft.com/products/ai-services/ai-inference", "PowerToys_AdvancedPaste_AdvancedAI_AzureAIInference"),
            AIServiceType.Mistral => ("https://console.mistral.ai/account/api-keys", "PowerToys_AdvancedPaste_AdvancedAI_Mistral"),
            AIServiceType.Google => ("https://ai.google.dev/", "PowerToys_AdvancedPaste_AdvancedAI_Google"),
            AIServiceType.HuggingFace => ("https://huggingface.co/settings/tokens", "PowerToys_AdvancedPaste_AdvancedAI_HuggingFace"),
            AIServiceType.Ollama => null,
            AIServiceType.Anthropic => null,
            AIServiceType.AmazonBedrock => null,
            _ => null,
        };
    }

    private static (string Resource, string Username)? GetPasteAiEntry(AIServiceType serviceType, string providerId)
    {
        string resource;
        string serviceKey;

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                resource = "https://platform.openai.com/api-keys";
                serviceKey = "openai";
                break;
            case AIServiceType.AzureOpenAI:
                resource = "https://azure.microsoft.com/products/ai-services/openai-service";
                serviceKey = "azureopenai";
                break;
            case AIServiceType.AzureAIInference:
                resource = "https://azure.microsoft.com/products/ai-services/ai-inference";
                serviceKey = "azureaiinference";
                break;
            case AIServiceType.Mistral:
                resource = "https://console.mistral.ai/account/api-keys";
                serviceKey = "mistral";
                break;
            case AIServiceType.Google:
                resource = "https://ai.google.dev/";
                serviceKey = "google";
                break;
            case AIServiceType.HuggingFace:
                resource = "https://huggingface.co/settings/tokens";
                serviceKey = "huggingface";
                break;
            case AIServiceType.FoundryLocal:
            case AIServiceType.ML:
            case AIServiceType.Onnx:
            case AIServiceType.Ollama:
            case AIServiceType.Anthropic:
            case AIServiceType.AmazonBedrock:
                return null;
            default:
                return null;
        }

        string username = $"PowerToys_AdvancedPaste_PasteAI_{serviceKey}_{NormalizeProviderIdentifier(providerId)}";
        return (resource, username);
    }

    private static string NormalizeProviderIdentifier(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return "default";
        }

        var filtered = new string(providerId.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "default" : filtered.ToLowerInvariant();
    }
}
