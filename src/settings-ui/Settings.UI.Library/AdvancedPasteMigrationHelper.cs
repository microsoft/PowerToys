// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Helper methods for migrating legacy Advanced Paste settings to the updated schema.
    /// </summary>
    public static class AdvancedPasteMigrationHelper
    {
        /// <summary>
        /// Ensures an OpenAI provider exists in the configuration, creating one if necessary.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The ensured provider and a flag indicating whether changes were made.</returns>
        public static (PasteAIProviderDefinition Provider, bool Updated) EnsureOpenAIProvider(PasteAIConfiguration configuration)
        {
            if (configuration is null)
            {
                return (null, false);
            }

            configuration.Providers ??= new ObservableCollection<PasteAIProviderDefinition>();

            const string serviceTypeKey = "OpenAI";
            var existingProvider = configuration.Providers.FirstOrDefault(provider => string.Equals(provider.ServiceType, serviceTypeKey, StringComparison.OrdinalIgnoreCase));
            bool updated = false;

            if (existingProvider is null)
            {
                existingProvider = CreateProvider(serviceTypeKey);
                configuration.Providers.Add(existingProvider);
                updated = true;
            }

            updated |= EnsureActiveProviderIsValid(configuration, existingProvider);

            return (existingProvider, updated);
        }

        /// <summary>
        /// Creates a provider with default values for the requested service type.
        /// </summary>
        private static PasteAIProviderDefinition CreateProvider(string serviceTypeKey)
        {
            var serviceType = serviceTypeKey.ToAIServiceType();
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            var provider = new PasteAIProviderDefinition
            {
                ServiceType = serviceTypeKey,
                ModelName = PasteAIProviderDefaults.GetDefaultModelName(serviceType),
                EndpointUrl = string.Empty,
                ApiVersion = string.Empty,
                DeploymentName = string.Empty,
                ModelPath = string.Empty,
                SystemPrompt = string.Empty,
                ModerationEnabled = serviceType == AIServiceType.OpenAI,
                IsLocalModel = metadata.IsLocalModel,
            };

            return provider;
        }

        private static bool EnsureActiveProviderIsValid(PasteAIConfiguration configuration, PasteAIProviderDefinition preferredProvider = null)
        {
            if (configuration?.Providers is null || configuration.Providers.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(configuration?.ActiveProviderId))
                {
                    configuration.ActiveProviderId = string.Empty;
                    return true;
                }

                return false;
            }

            bool updated = false;

            var activeProvider = configuration.Providers.FirstOrDefault(provider => string.Equals(provider.Id, configuration.ActiveProviderId, StringComparison.OrdinalIgnoreCase));
            if (activeProvider is null)
            {
                activeProvider = preferredProvider ?? configuration.Providers.First();
                configuration.ActiveProviderId = activeProvider.Id;
                updated = true;
            }

            foreach (var provider in configuration.Providers)
            {
                bool shouldBeActive = string.Equals(provider.Id, configuration.ActiveProviderId, StringComparison.OrdinalIgnoreCase);
                if (provider.IsActive != shouldBeActive)
                {
                    provider.IsActive = shouldBeActive;
                    updated = true;
                }
            }

            return updated;
        }
    }
}
