// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.Security.Credentials;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Reads the API key that Advanced Paste stored in the Windows Credential Vault for a given
    /// provider. The resource/username scheme must stay in sync with Advanced Paste's
    /// EnhancedVaultCredentialsProvider, since both modules share one provider configuration.
    /// </summary>
    internal static class AICredentialsReader
    {
        public static string GetKey(AIServiceType serviceType, string? providerId)
        {
            var entry = BuildCredentialEntry(serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType, providerId ?? string.Empty);
            if (entry is null)
            {
                return string.Empty;
            }

            try
            {
                var credential = new PasswordVault().Retrieve(entry.Value.Resource, entry.Value.Username);
                credential?.RetrievePassword();
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
                default:
                    return null;
            }

            return (resource, $"PowerToys_AdvancedPaste_PasteAI_{serviceKey}_{NormalizeProviderIdentifier(providerId)}");
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
}
