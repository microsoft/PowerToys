// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        var desiredServiceType = NormalizeServiceType(ResolveServiceType(scope));

        var hasChanged = false;

        if (slot.ServiceType != desiredServiceType)
        {
            slot.ServiceType = desiredServiceType;
            slot.Entry = BuildCredentialEntry(desiredServiceType, scope);
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

    private AIServiceType ResolveServiceType(AICredentialScope scope)
    {
        return scope switch
        {
            AICredentialScope.AdvancedAI => _userSettings.AdvancedAIConfiguration?.ServiceTypeKind ?? AIServiceType.OpenAI,
            AICredentialScope.PasteAI => _userSettings.PasteAIConfiguration?.ServiceTypeKind ?? AIServiceType.OpenAI,
            _ => AIServiceType.OpenAI,
        };
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

    private static (string Resource, string Username)? BuildCredentialEntry(AIServiceType serviceType, AICredentialScope scope)
    {
        return scope switch
        {
            AICredentialScope.AdvancedAI => GetAdvancedAiEntry(serviceType),
            AICredentialScope.PasteAI => GetPasteAiEntry(serviceType),
            _ => null,
        };
    }

    private static (string Resource, string Username)? GetAdvancedAiEntry(AIServiceType serviceType)
    {
        var normalizedKey = serviceType.ToNormalizedKey();

        return serviceType switch
        {
            AIServiceType.OpenAI => ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_AdvancedAI_OpenAI"),
            AIServiceType.AzureOpenAI => ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_AdvancedAI_AzureOpenAI"),
            _ => null,
        };
    }

    private static (string Resource, string Username)? GetPasteAiEntry(AIServiceType serviceType)
    {
        return serviceType switch
        {
            AIServiceType.OpenAI => ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_PasteAI_OpenAI"),
            AIServiceType.AzureOpenAI => ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_PasteAI_AzureOpenAI"),
            _ => null,
        };
    }
}
