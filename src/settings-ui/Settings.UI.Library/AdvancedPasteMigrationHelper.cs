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
        /// Moves legacy provider configuration snapshots into the strongly-typed providers collection.
        /// </summary>
        /// <param name="configuration">The configuration instance to migrate.</param>
        /// <returns>True if the configuration was modified.</returns>
        public static bool MigrateLegacyProviderConfigurations(PasteAIConfiguration configuration)
        {
            if (configuration is null)
            {
                return false;
            }

            configuration.Providers ??= new ObservableCollection<PasteAIProviderDefinition>();

            bool configurationUpdated = false;

            if (configuration.LegacyProviderConfigurations is { Count: > 0 })
            {
                foreach (var entry in configuration.LegacyProviderConfigurations)
                {
                    var result = EnsureProvider(configuration, entry.Key, entry.Value);
                    configurationUpdated |= result.Updated;
                }

                configuration.LegacyProviderConfigurations = null;
            }

            configurationUpdated |= EnsureActiveProviderIsValid(configuration);

            return configurationUpdated;
        }

        /// <summary>
        /// Ensures an OpenAI provider exists in the configuration, creating one if necessary.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The ensured provider and a flag indicating whether changes were made.</returns>
        public static (PasteAIProviderDefinition Provider, bool Updated) EnsureOpenAIProvider(PasteAIConfiguration configuration)
        {
            return EnsureProvider(configuration, AIServiceType.OpenAI.ToConfigurationString(), null);
        }

        /// <summary>
        /// Ensures a provider for the supplied service type exists, optionally applying a legacy snapshot.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <param name="serviceTypeKey">The persisted service type key.</param>
        /// <param name="snapshot">An optional snapshot containing legacy values.</param>
        /// <returns>The ensured provider and whether the configuration was updated.</returns>
        public static (PasteAIProviderDefinition Provider, bool Updated) EnsureProvider(PasteAIConfiguration configuration, string serviceTypeKey, AIProviderConfigurationSnapshot snapshot)
        {
            if (configuration is null)
            {
                return (null, false);
            }

            configuration.Providers ??= new ObservableCollection<PasteAIProviderDefinition>();

            var normalizedServiceType = NormalizeServiceType(serviceTypeKey);
            var existingProvider = configuration.Providers.FirstOrDefault(provider => string.Equals(provider.ServiceType, normalizedServiceType, StringComparison.OrdinalIgnoreCase));
            bool configurationUpdated = false;

            if (existingProvider is null)
            {
                existingProvider = CreateProvider(normalizedServiceType, snapshot);
                configuration.Providers.Add(existingProvider);
                configurationUpdated = true;
            }
            else if (snapshot is not null)
            {
                configurationUpdated |= ApplySnapshot(existingProvider, snapshot);
            }

            configurationUpdated |= EnsureActiveProviderIsValid(configuration, existingProvider);

            return (existingProvider, configurationUpdated);
        }

        private static string NormalizeServiceType(string serviceTypeKey)
        {
            var serviceType = serviceTypeKey.ToAIServiceType();
            return serviceType.ToConfigurationString();
        }

        private static PasteAIProviderDefinition CreateProvider(string serviceTypeKey, AIProviderConfigurationSnapshot snapshot)
        {
            var serviceType = serviceTypeKey.ToAIServiceType();
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            var provider = new PasteAIProviderDefinition
            {
                ServiceType = serviceTypeKey,
                ModelName = !string.IsNullOrWhiteSpace(snapshot?.ModelName) ? snapshot.ModelName : PasteAIProviderDefaults.GetDefaultModelName(serviceType),
                EndpointUrl = snapshot?.EndpointUrl ?? string.Empty,
                ApiVersion = snapshot?.ApiVersion ?? string.Empty,
                DeploymentName = snapshot?.DeploymentName ?? string.Empty,
                ModelPath = snapshot?.ModelPath ?? string.Empty,
                SystemPrompt = snapshot?.SystemPrompt ?? string.Empty,
                ModerationEnabled = snapshot?.ModerationEnabled ?? true,
                IsLocalModel = metadata.IsLocalModel,
            };

            return provider;
        }

        private static bool ApplySnapshot(PasteAIProviderDefinition provider, AIProviderConfigurationSnapshot snapshot)
        {
            bool updated = false;

            if (!string.IsNullOrWhiteSpace(snapshot.ModelName) && !string.Equals(provider.ModelName, snapshot.ModelName, StringComparison.Ordinal))
            {
                provider.ModelName = snapshot.ModelName;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.EndpointUrl) && !string.Equals(provider.EndpointUrl, snapshot.EndpointUrl, StringComparison.Ordinal))
            {
                provider.EndpointUrl = snapshot.EndpointUrl;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ApiVersion) && !string.Equals(provider.ApiVersion, snapshot.ApiVersion, StringComparison.Ordinal))
            {
                provider.ApiVersion = snapshot.ApiVersion;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.DeploymentName) && !string.Equals(provider.DeploymentName, snapshot.DeploymentName, StringComparison.Ordinal))
            {
                provider.DeploymentName = snapshot.DeploymentName;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ModelPath) && !string.Equals(provider.ModelPath, snapshot.ModelPath, StringComparison.Ordinal))
            {
                provider.ModelPath = snapshot.ModelPath;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SystemPrompt) && !string.Equals(provider.SystemPrompt, snapshot.SystemPrompt, StringComparison.Ordinal))
            {
                provider.SystemPrompt = snapshot.SystemPrompt;
                updated = true;
            }

            if (provider.ModerationEnabled != snapshot.ModerationEnabled)
            {
                provider.ModerationEnabled = snapshot.ModerationEnabled;
                updated = true;
            }

            return updated;
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
