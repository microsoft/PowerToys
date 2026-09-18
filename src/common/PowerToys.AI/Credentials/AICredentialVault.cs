// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.AI.Credentials;

/// <summary>
/// Credential vault helpers for PowerToys AI providers.
/// </summary>
/// <remarks>
/// Preferred username format: <c>PowerToys_AI_{service}_{providerId}</c>.
/// Legacy Advanced Paste usernames (<c>PowerToys_AdvancedPaste_PasteAI_...</c>) are read as a fallback
/// and copied to the new name on first successful load.
/// </remarks>
public static class AICredentialVault
{
    public const string UsernamePrefix = "PowerToys_AI_";
    public const string LegacyAdvancedPasteUsernamePrefix = "PowerToys_AdvancedPaste_PasteAI_";

    public static bool RequiresApiKey(AIServiceType serviceType) =>
        serviceType is AIServiceType.OpenAI
            or AIServiceType.AzureOpenAI
            or AIServiceType.AzureAIInference
            or AIServiceType.Mistral
            or AIServiceType.Google;

    public static (string Resource, string Username)? BuildCredentialEntry(AIServiceType serviceType, string providerId, bool legacy = false)
    {
        if (!TryGetResource(serviceType, out var resource, out var serviceKey))
        {
            return null;
        }

        var prefix = legacy ? LegacyAdvancedPasteUsernamePrefix : UsernamePrefix;
        var username = $"{prefix}{serviceKey}_{NormalizeProviderIdentifier(providerId)}";
        return (resource, username);
    }

    public static string LoadKey(AIServiceType serviceType, string providerId)
    {
        var entry = BuildCredentialEntry(serviceType, providerId);
        if (entry is null)
        {
            return string.Empty;
        }

        var key = Retrieve(entry.Value.Resource, entry.Value.Username);
        if (!string.IsNullOrEmpty(key))
        {
            return key;
        }

        // One-time migration from Advanced Paste vault usernames.
        var legacy = BuildCredentialEntry(serviceType, providerId, legacy: true);
        if (legacy is null)
        {
            return string.Empty;
        }

        key = Retrieve(legacy.Value.Resource, legacy.Value.Username);
        if (!string.IsNullOrEmpty(key))
        {
            try
            {
                SaveKey(serviceType, providerId, key);
            }
            catch
            {
                // Best-effort migration; still return the legacy key.
            }
        }

        return key;
    }

    public static void SaveKey(AIServiceType serviceType, string providerId, string apiKey)
    {
        var entry = BuildCredentialEntry(serviceType, providerId);
        if (entry is null)
        {
            return;
        }

        var vault = new PasswordVault();
        try
        {
            var existing = vault.Retrieve(entry.Value.Resource, entry.Value.Username);
            if (existing is not null)
            {
                vault.Remove(existing);
            }
        }
        catch
        {
            // No existing credential.
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            vault.Add(new PasswordCredential(entry.Value.Resource, entry.Value.Username, apiKey));
        }
    }

    public static string NormalizeProviderIdentifier(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return "default";
        }

        var filtered = new string(providerId.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "default" : filtered.ToLowerInvariant();
    }

    private static bool TryGetResource(AIServiceType serviceType, out string resource, out string serviceKey)
    {
        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                resource = "https://platform.openai.com/api-keys";
                serviceKey = "openai";
                return true;
            case AIServiceType.AzureOpenAI:
                resource = "https://azure.microsoft.com/products/ai-services/openai-service";
                serviceKey = "azureopenai";
                return true;
            case AIServiceType.AzureAIInference:
                resource = "https://azure.microsoft.com/products/ai-services/ai-inference";
                serviceKey = "azureaiinference";
                return true;
            case AIServiceType.Mistral:
                resource = "https://console.mistral.ai/account/api-keys";
                serviceKey = "mistral";
                return true;
            case AIServiceType.Google:
                resource = "https://ai.google.dev/";
                serviceKey = "google";
                return true;
            default:
                resource = string.Empty;
                serviceKey = string.Empty;
                return false;
        }
    }

    private static string Retrieve(string resource, string username)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(resource, username);
            credential?.RetrievePassword();
            return credential?.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
