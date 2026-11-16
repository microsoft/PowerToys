// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Resolves the AI provider Robocopy UI should use. Robocopy UI intentionally reuses the
    /// provider that the user already configured for Advanced Paste so no extra setup is needed.
    /// </summary>
    public sealed class AIProviderResolver
    {
        private const string AdvancedPasteModuleName = "AdvancedPaste";

        /// <summary>
        /// Every chat backend Robocopy UI can drive, paired with the service types it handles. This
        /// mirrors Advanced Paste's provider registration list so both modules accept the same set of
        /// configured providers; add a new backend here and it becomes resolvable automatically.
        /// </summary>
        private static readonly IReadOnlyList<(IReadOnlyCollection<AIServiceType> SupportedTypes, Func<AIProviderConfig, IAIChatProvider> Factory)> ProviderRegistrations = new[]
        {
            (SemanticKernelChatProvider.SupportedTypes, new Func<AIProviderConfig, IAIChatProvider>(config => new SemanticKernelChatProvider(config))),
            (FoundryLocalChatProvider.SupportedTypes, new Func<AIProviderConfig, IAIChatProvider>(config => new FoundryLocalChatProvider(config))),
            (PhiSilicaChatProvider.SupportedTypes, new Func<AIProviderConfig, IAIChatProvider>(config => new PhiSilicaChatProvider(config))),
            (LocalModelChatProvider.SupportedTypes, new Func<AIProviderConfig, IAIChatProvider>(config => new LocalModelChatProvider(config))),
        };

        private static readonly IReadOnlyDictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>> ProviderFactories = CreateProviderFactories();

        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;

        public bool IsConfigured()
        {
            try
            {
                return TryResolve(out _, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves the active provider configuration.
        /// </summary>
        /// <param name="config">The resolved configuration, or null when no usable provider exists.</param>
        /// <param name="displayName">A human readable name for the resolved provider.</param>
        /// <returns>True when a usable provider was resolved.</returns>
        public bool TryResolve(out AIProviderConfig? config, out string displayName)
            => TryResolve(providerId: null, out config, out displayName);

        /// <summary>
        /// Resolves a specific provider by id, falling back to the active provider when no id is
        /// given or the requested one no longer exists.
        /// </summary>
        /// <param name="providerId">Id of the provider to resolve, or null for the active provider.</param>
        /// <param name="config">The resolved configuration, or null when no usable provider exists.</param>
        /// <param name="displayName">A human readable name for the resolved provider.</param>
        /// <returns>True when a usable provider was resolved.</returns>
        public bool TryResolve(string? providerId, out AIProviderConfig? config, out string displayName)
        {
            config = null;
            displayName = string.Empty;

            PasteAIProviderDefinition? provider = null;

            try
            {
                var configuration = ReadConfiguration();

                if (!string.IsNullOrWhiteSpace(providerId))
                {
                    provider = configuration?.Providers?
                        .FirstOrDefault(candidate => string.Equals(candidate.Id, providerId, StringComparison.OrdinalIgnoreCase));
                }

                // Falls back when the caller passed no id, or when the selected provider was removed
                // from settings while the dialog was open.
                provider ??= configuration?.ActiveProvider;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read AI provider configuration", ex);
                return false;
            }

            if (provider is null)
            {
                return false;
            }

            var serviceType = NormalizeServiceType(provider.ServiceTypeKind);

            if (!IsSupported(serviceType))
            {
                return false;
            }

            config = CreateConfig(provider, serviceType);
            displayName = provider.DisplayName;
            return true;
        }

        /// <summary>
        /// Lists the configured providers that Robocopy UI can actually run, so the dialog can offer
        /// a choice between them.
        /// </summary>
        /// <param name="activeProviderId">Id of the provider currently marked active, or empty when there is none.</param>
        public IReadOnlyList<PasteAIProviderDefinition> GetSupportedProviders(out string activeProviderId)
        {
            activeProviderId = string.Empty;

            try
            {
                var configuration = ReadConfiguration();

                if (configuration?.Providers is null)
                {
                    return [];
                }

                activeProviderId = configuration.ActiveProvider?.Id ?? string.Empty;

                return configuration.Providers
                    .Where(provider => IsSupported(NormalizeServiceType(provider.ServiceTypeKind)))
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enumerate AI providers", ex);
                return [];
            }
        }

        /// <summary>
        /// Marks a provider as the active one in Advanced Paste's settings, which is the same store
        /// Advanced Paste's own picker writes to, so the choice is shared between the two modules.
        /// </summary>
        /// <returns>True when the settings file was updated.</returns>
        public bool SetActiveProvider(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return false;
            }

            try
            {
                if (!_settingsUtils.SettingsExists(AdvancedPasteModuleName))
                {
                    return false;
                }

                var settings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName);
                var configuration = settings?.Properties?.PasteAIConfiguration;
                var providers = configuration?.Providers;

                if (settings is null || configuration is null || providers is null || providers.Count == 0)
                {
                    return false;
                }

                if (!providers.Any(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                if (string.Equals(configuration.ActiveProvider?.Id, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                configuration.ActiveProviderId = providerId;

                // IsActive is not serialized, but Advanced Paste keeps the flags consistent when it
                // writes this file, so mirror that rather than leaving a half-updated object behind.
                foreach (var provider in providers)
                {
                    provider.IsActive = string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase);
                }

                settings.Save(_settingsUtils);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set the active AI provider", ex);
                return false;
            }
        }

        private PasteAIConfiguration? ReadConfiguration()
        {
            if (!_settingsUtils.SettingsExists(AdvancedPasteModuleName))
            {
                return null;
            }

            return _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName)?.Properties?.PasteAIConfiguration;
        }

        private static AIProviderConfig CreateConfig(PasteAIProviderDefinition provider, AIServiceType serviceType) => new()
        {
            ProviderType = serviceType,
            Model = provider.ModelName,
            Endpoint = provider.EndpointUrl,
            DeploymentName = provider.DeploymentName,
            ModelPath = provider.ModelPath,
            ApiKey = AICredentialsReader.GetKey(serviceType, provider.Id),
        };

        private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
            => serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;

        public static IAIChatProvider CreateProvider(AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var serviceType = config.ProviderType == AIServiceType.Unknown ? AIServiceType.OpenAI : config.ProviderType;

            if (!ProviderFactories.TryGetValue(serviceType, out var factory))
            {
                throw new AIGenerationException($"Provider '{config.ProviderType}' is not supported by Robocopy UI.");
            }

            return factory(config);
        }

        private static IReadOnlyDictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>> CreateProviderFactories()
        {
            var map = new Dictionary<AIServiceType, Func<AIProviderConfig, IAIChatProvider>>();

            foreach (var (supportedTypes, factory) in ProviderRegistrations)
            {
                foreach (var type in supportedTypes)
                {
                    map[type] = factory;
                }
            }

            return map;
        }

        private static bool IsSupported(AIServiceType serviceType) => ProviderFactories.ContainsKey(serviceType);
    }
}
