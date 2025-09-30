// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Security.Credentials;

namespace AdvancedPaste.Services;

/// <summary>
/// Enhanced credentials provider that supports different AI service types
/// Keys are stored in Windows Credential Vault with service-specific identifiers
/// </summary>
public sealed class EnhancedVaultCredentialsProvider : IAICredentialsProvider
{
    private readonly string _serviceType;
    private readonly (string Resource, string Username)[] _credentialEntries;
    private readonly (string Resource, string Username) _primaryEntry;

    public EnhancedVaultCredentialsProvider(string serviceType, string credentialScope)
    {
        _serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialScope);

        _credentialEntries = BuildCredentialEntries(_serviceType, credentialScope).ToArray();
        _primaryEntry = _credentialEntries.FirstOrDefault();

        Key = LoadKey();
    }

    public string Key { get; private set; }

    public bool IsConfigured => !string.IsNullOrEmpty(Key);

    public bool Refresh()
    {
        var oldKey = Key;
        Key = LoadKey();
        return oldKey != Key;
    }

    private string LoadKey()
    {
        if (_credentialEntries.Length == 0)
        {
            return string.Empty;
        }

        var vault = new PasswordVault();

        foreach (var entry in _credentialEntries)
        {
            try
            {
                var credential = vault.Retrieve(entry.Resource, entry.Username);
                if (!string.IsNullOrEmpty(credential?.Password))
                {
                    return credential.Password;
                }
            }
            catch (Exception)
            {
                // Ignore and try next mapping
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Save credentials to the vault (for use by settings UI)
    /// </summary>
    public void SaveKey(string key)
    {
        try
        {
            var vault = new PasswordVault();

            RemoveExistingCredentials(vault);

            if (_primaryEntry != default && !string.IsNullOrWhiteSpace(key))
            {
                vault.Add(new PasswordCredential(_primaryEntry.Resource, _primaryEntry.Username, key));
            }

            Key = key ?? string.Empty;
        }
        catch (Exception)
        {
            // Handle gracefully - credential might not be saveable in some environments
        }
    }

    private void RemoveExistingCredentials(PasswordVault vault)
    {
        if (_credentialEntries.Length == 0)
        {
            return;
        }

        foreach (var entry in _credentialEntries)
        {
            try
            {
                var existing = vault.Retrieve(entry.Resource, entry.Username);
                if (existing != null)
                {
                    vault.Remove(existing);
                }
            }
            catch
            {
                // Ignore if not found
            }
        }
    }

    private static IEnumerable<(string Resource, string Username)> BuildCredentialEntries(string serviceType, string credentialScope)
    {
        string normalizedScope = credentialScope.Trim().ToLowerInvariant();
        string normalizedType = NormalizeServiceType(serviceType);

        var entries = new List<(string Resource, string Username)>();

        // Primary mappings align with settings UI save logic
        if (normalizedScope == "advancedai")
        {
            entries.AddRange(GetAdvancedAiEntries(normalizedType));
        }
        else
        {
            entries.AddRange(GetPasteAiEntries(normalizedType));
        }

        // Backward compatibility with previous credential naming
        entries.AddRange(GetFallbackEntries(normalizedType));

        // Ensure distinct entries preserving order
        return entries.Distinct();
    }

    private static IEnumerable<(string Resource, string Username)> GetAdvancedAiEntries(string normalizedType)
    {
        return normalizedType switch
        {
            "openai" => new[] { ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_AdvancedAI_OpenAI") },
            "azureopenai" => new[] { ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_AdvancedAI_AzureOpenAI") },
            _ => new[] { ($"https://ai-service/advanced/{normalizedType}", $"PowerToys_AdvancedPaste_AdvancedAI_{normalizedType.ToUpperInvariant()}") },
        };
    }

    private static IEnumerable<(string Resource, string Username)> GetPasteAiEntries(string normalizedType)
    {
        return normalizedType switch
        {
            "openai" => new[] { ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_PasteAI_OpenAI") },
            "azureopenai" => new[] { ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_PasteAI_AzureOpenAI") },
            "onnx" => Array.Empty<(string, string)>(),
            _ => new[] { ($"https://ai-service/paste/{normalizedType}", $"PowerToys_AdvancedPaste_PasteAI_{normalizedType.ToUpperInvariant()}") },
        };
    }

    private static IEnumerable<(string Resource, string Username)> GetFallbackEntries(string normalizedType)
    {
        return normalizedType switch
        {
            "openai" => new[]
            {
                ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey"),
                ("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_openaiKey"),
            },
            "azureopenai" => new[]
            {
                ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_AzureOpenAIKey"),
                ("https://portal.azure.com/openai", "PowerToys_AdvancedPaste_AzureOpenAIKey"),
                ("https://azure.microsoft.com/products/ai-services/openai-service", "PowerToys_AdvancedPaste_azureopenaiKey"),
            },
            _ => Array.Empty<(string, string)>(),
        };
    }

    private static string NormalizeServiceType(string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            return "openai";
        }

        var normalized = serviceType.Trim().ToLowerInvariant();

        return normalized switch
        {
            "azure" => "azureopenai",
            _ => normalized,
        };
    }
}
