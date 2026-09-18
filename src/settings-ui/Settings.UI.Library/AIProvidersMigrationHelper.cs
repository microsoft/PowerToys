// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Migrates Advanced Paste paste-ai-configuration into the global AIProviders store.
    /// </summary>
    public static class AIProvidersMigrationHelper
    {
        /// <summary>
        /// Ensures global AI provider settings exist, copying from Advanced Paste when needed.
        /// </summary>
        /// <returns>True when a migration write occurred.</returns>
        public static bool EnsureMigrated(SettingsUtils settingsUtils)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            AIProvidersSettings globalSettings;
            try
            {
                globalSettings = settingsUtils.GetSettingsOrDefault<AIProvidersSettings>(AIProvidersSettings.ModuleName);
            }
            catch
            {
                globalSettings = new AIProvidersSettings();
            }

            globalSettings.Properties ??= new AIProvidersProperties();
            globalSettings.Properties.AIConfiguration ??= new AIConfiguration();

            if (globalSettings.Properties.AIConfiguration.Providers is { Count: > 0 })
            {
                return false;
            }

            AdvancedPasteSettings advancedPasteSettings;
            try
            {
                advancedPasteSettings = settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteSettings.ModuleName);
            }
            catch
            {
                return false;
            }

            var source = advancedPasteSettings?.Properties?.PasteAIConfiguration;
            if (source?.Providers is null || source.Providers.Count == 0)
            {
                return false;
            }

            var migrated = new AIConfiguration
            {
                ActiveProviderId = source.ActiveProviderId,
                Providers = new ObservableCollection<AIProviderDefinition>(
                    source.Providers.Select(CloneProvider)),
            };

            globalSettings.Properties.AIConfiguration = migrated;
            globalSettings.Save(settingsUtils);
            return true;
        }

        private static AIProviderDefinition CloneProvider(AIProviderDefinition provider)
        {
            if (provider is null)
            {
                return new AIProviderDefinition();
            }

            // Deep-clone via JSON so obsolete PasteAIProviderDefinition instances copy cleanly.
            var json = JsonSerializer.Serialize(provider, SettingsSerializationContext.Default.AIProviderDefinition);
            return JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.AIProviderDefinition)
                ?? provider.Clone();
        }
    }
}
