// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
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
    private readonly CredentialSlot _slot;
    private readonly Lock _syncRoot = new();

    public EnhancedVaultCredentialsProvider(IUserSettings userSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));

        _slot = new CredentialSlot();

        Refresh();
    }

    public string GetKey()
    {
        using (_syncRoot.EnterScope())
        {
            UpdateSlot(forceRefresh: false);
            return _slot.Key;
        }
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(GetKey());
    }

    public bool Refresh()
    {
        using (_syncRoot.EnterScope())
        {
            return UpdateSlot(forceRefresh: true);
        }
    }

    private bool UpdateSlot(bool forceRefresh)
    {
        var (serviceType, providerId) = ResolveCredentialTarget();
        var desiredServiceType = NormalizeServiceType(serviceType);
        providerId ??= string.Empty;

        var hasChanged = false;

        if (_slot.ServiceType != desiredServiceType || !string.Equals(_slot.ProviderId, providerId, StringComparison.Ordinal))
        {
            _slot.ServiceType = desiredServiceType;
            _slot.ProviderId = providerId;
            _slot.Entry = BuildCredentialEntry(desiredServiceType, providerId);
            forceRefresh = true;
            hasChanged = true;
        }

        if (!forceRefresh)
        {
            return hasChanged;
        }

        var newKey = LoadKey(_slot.Entry);
        if (!string.Equals(_slot.Key, newKey, StringComparison.Ordinal))
        {
            _slot.Key = newKey;
            hasChanged = true;
        }

        return hasChanged;
    }

    private (AIServiceType ServiceType, string ProviderId) ResolveCredentialTarget()
    {
        var provider = _userSettings.PasteAIConfiguration?.ActiveProvider;
        if (provider is null)
        {
            return (AIServiceType.OpenAI, string.Empty);
        }

        return (provider.ServiceTypeKind, provider.Id ?? string.Empty);
    }

    private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
    {
        return serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
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

    private static (string Resource, string Username)? BuildCredentialEntry(AIServiceType serviceType, string providerId)
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
