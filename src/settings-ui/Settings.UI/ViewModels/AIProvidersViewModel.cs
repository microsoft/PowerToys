// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.AI.Credentials;
using Microsoft.PowerToys.AI.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AIProvidersViewModel : PageViewModelBase
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly AIProvidersSettings _aiProvidersSettings;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Func<string, int> _sendConfigMSG;

        private bool _disposed;
        private AIProviderDefinition _providerDraft;
        private AIProviderDefinition _editingProvider;
        private bool _suppressSave;

        private GpoRuleConfigured _onlineAIModelsGpoRuleConfiguration;
        private bool _onlineAIModelsDisallowedByGPO;

        protected override string ModuleName => AIProvidersSettings.ModuleName;

        public AIProvidersViewModel(
            SettingsUtils settingsUtils,
            Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _sendConfigMSG = ipcMSGCallBackFunc ?? throw new ArgumentNullException(nameof(ipcMSGCallBackFunc));
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            bool migrated = AIProvidersMigrationHelper.EnsureMigrated(_settingsUtils);
            if (migrated)
            {
                PowerToysTelemetry.Log.WriteEvent(new AISettingsMigrationEvent(
                    AIProvidersSettings.ModuleName,
                    "AdvancedPasteToGlobal",
                    succeeded: true));
            }

            try
            {
                _aiProvidersSettings = _settingsUtils.GetSettingsOrDefault<AIProvidersSettings>(AIProvidersSettings.ModuleName);
            }
            catch
            {
                _aiProvidersSettings = new AIProvidersSettings();
            }

            _aiProvidersSettings.Properties ??= new AIProvidersProperties();
            _aiProvidersSettings.Properties.AIConfiguration ??= new AIConfiguration();
            _aiProvidersSettings.Properties.AIConfiguration.Providers ??= new ObservableCollection<AIProviderDefinition>();

            InitializeGpoState();
            InitializeProviderState();
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            return new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = Array.Empty<HotkeySettings>(),
            };
        }

        public static IEnumerable<AIServiceTypeMetadata> AvailableProviders => AIServiceTypeRegistry.GetAvailableServiceTypes();

        public IEnumerable<AIServiceTypeMetadata> AvailableProvidersFilteredByGPO =>
            AvailableProviders.Where(metadata => IsServiceTypeAllowedByGPO(metadata.ServiceType));

        public bool IsOnlineAIModelsDisallowedByGPO => _onlineAIModelsDisallowedByGPO;

        public bool ShowOnlineAIModelsGpoConfiguredInfoBar => _onlineAIModelsDisallowedByGPO;

        public AIConfiguration AIConfiguration
        {
            get
            {
                _aiProvidersSettings.Properties.AIConfiguration ??= new AIConfiguration();
                return _aiProvidersSettings.Properties.AIConfiguration;
            }

            set
            {
                if (!ReferenceEquals(value, _aiProvidersSettings.Properties.AIConfiguration))
                {
                    UnsubscribeFromConfiguration(_aiProvidersSettings.Properties.AIConfiguration);

                    var newValue = value ?? new AIConfiguration();
                    _aiProvidersSettings.Properties.AIConfiguration = newValue;
                    SyncProviderActiveFlags(newValue);
                    SubscribeToConfiguration(newValue);

                    OnPropertyChanged(nameof(AIConfiguration));
                    SaveAndNotifySettings();
                }
            }
        }

        public AIProviderDefinition ProviderDraft
        {
            get => _providerDraft;
            private set
            {
                if (!ReferenceEquals(_providerDraft, value))
                {
                    _providerDraft = value;
                    OnPropertyChanged(nameof(ProviderDraft));
                    OnPropertyChanged(nameof(ShowProviderGpoConfiguredInfoBar));
                }
            }
        }

        public bool ShowProviderGpoConfiguredInfoBar
        {
            get
            {
                if (_providerDraft is null)
                {
                    return false;
                }

                var serviceType = _providerDraft.ServiceType.ToAIServiceType();
                return !IsServiceTypeAllowedByGPO(serviceType);
            }
        }

        public bool IsEditingProvider => _editingProvider is not null;

        public void RefreshEnabledState()
        {
            InitializeGpoState();
            OnPropertyChanged(nameof(IsOnlineAIModelsDisallowedByGPO));
            OnPropertyChanged(nameof(ShowOnlineAIModelsGpoConfiguredInfoBar));
            OnPropertyChanged(nameof(AvailableProvidersFilteredByGPO));
        }

        public void ReloadFromDisk()
        {
            AIProvidersSettings latest;
            try
            {
                latest = _settingsUtils.GetSettingsOrDefault<AIProvidersSettings>(AIProvidersSettings.ModuleName);
            }
            catch
            {
                return;
            }

            if (latest?.Properties?.AIConfiguration is null)
            {
                return;
            }

            try
            {
                _suppressSave = true;
                AIConfiguration = latest.Properties.AIConfiguration;
            }
            finally
            {
                _suppressSave = false;
            }
        }

        public void BeginAddProvider(string serviceType)
        {
            var normalizedServiceType = NormalizeServiceType(serviceType, out var persistedServiceType);
            var metadata = AIServiceTypeRegistry.GetMetadata(normalizedServiceType);
            var provider = new AIProviderDefinition
            {
                ServiceType = persistedServiceType,
                ModelName = AIProviderDefaults.GetDefaultModelName(normalizedServiceType),
                EndpointUrl = string.Empty,
                ApiVersion = string.Empty,
                DeploymentName = string.Empty,
                ModelPath = string.Empty,
                SystemPrompt = string.Empty,
                ModerationEnabled = normalizedServiceType == AIServiceType.OpenAI,
                IsLocalModel = metadata.IsLocalModel,
            };

            if (normalizedServiceType is AIServiceType.FoundryLocal or AIServiceType.Onnx or AIServiceType.ML)
            {
                provider.ModelName = string.Empty;
            }

            _editingProvider = null;
            ProviderDraft = provider;
        }

        public void BeginEditProvider(AIProviderDefinition provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            _editingProvider = provider;
            var draft = provider.Clone();
            var storedEndpoint = GetProviderEndpoint(draft.Id, draft.ServiceType);
            if (!string.IsNullOrWhiteSpace(storedEndpoint))
            {
                draft.EndpointUrl = storedEndpoint;
            }

            ProviderDraft = draft;
        }

        public void CancelProviderDraft()
        {
            ProviderDraft = null;
            _editingProvider = null;
        }

        public void CommitProviderDraft(string apiKey, string endpoint)
        {
            if (ProviderDraft is null)
            {
                return;
            }

            var config = AIConfiguration;
            var draft = ProviderDraft;
            draft.EndpointUrl = endpoint?.Trim() ?? string.Empty;

            SaveApiKey(draft.Id, draft.ServiceType, apiKey);

            string action;
            if (_editingProvider is null)
            {
                config.Providers.Add(draft);
                config.ActiveProviderId ??= draft.Id;
                action = "Added";
            }
            else
            {
                UpdateProviderFromDraft(_editingProvider, draft);
                draft = _editingProvider;
                _editingProvider = null;
                action = "Updated";
            }

            ProviderDraft = null;
            SyncProviderActiveFlags(config);
            SaveAndNotifySettings();
            OnPropertyChanged(nameof(AIConfiguration));

            PowerToysTelemetry.Log.WriteEvent(new AIProviderConfiguredEvent(
                AIProvidersSettings.ModuleName,
                draft.ServiceTypeKind,
                draft.Id,
                action,
                draft.IsLocalModel));
        }

        public void RemoveProvider(AIProviderDefinition provider)
        {
            if (provider is null)
            {
                return;
            }

            var config = AIConfiguration;
            if (config?.Providers is null)
            {
                return;
            }

            if (config.Providers.Remove(provider))
            {
                RemoveCredentials(provider.Id, provider.ServiceType);

                if (string.Equals(config.ActiveProviderId, provider.Id, StringComparison.OrdinalIgnoreCase))
                {
                    config.ActiveProviderId = config.Providers.FirstOrDefault()?.Id ?? string.Empty;
                }

                SyncProviderActiveFlags(config);
                SaveAndNotifySettings();
                OnPropertyChanged(nameof(AIConfiguration));

                PowerToysTelemetry.Log.WriteEvent(new AIProviderConfiguredEvent(
                    AIProvidersSettings.ModuleName,
                    provider.ServiceTypeKind,
                    provider.Id,
                    "Removed",
                    provider.IsLocalModel));
            }
        }

        public void SetAsDefaultProvider(AIProviderDefinition provider)
        {
            if (provider is null || string.IsNullOrEmpty(provider.Id))
            {
                return;
            }

            var config = AIConfiguration;
            if (config is null)
            {
                return;
            }

            config.ActiveProviderId = provider.Id;
            SyncProviderActiveFlags(config);
            SaveAndNotifySettings();
            OnPropertyChanged(nameof(AIConfiguration));

            PowerToysTelemetry.Log.WriteEvent(new AIProviderConfiguredEvent(
                AIProvidersSettings.ModuleName,
                provider.ServiceTypeKind,
                provider.Id,
                "SetActive",
                provider.IsLocalModel));
        }

        public bool IsServiceTypeAllowedByGPO(AIServiceType serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);

            if (metadata.IsOnlineService)
            {
                if (_onlineAIModelsGpoRuleConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                var individualGpoRule = serviceType switch
                {
                    AIServiceType.OpenAI => GPOWrapper.GetAllowedAdvancedPasteOpenAIValue(),
                    AIServiceType.AzureOpenAI => GPOWrapper.GetAllowedAdvancedPasteAzureOpenAIValue(),
                    AIServiceType.AzureAIInference => GPOWrapper.GetAllowedAdvancedPasteAzureAIInferenceValue(),
                    AIServiceType.Mistral => GPOWrapper.GetAllowedAdvancedPasteMistralValue(),
                    AIServiceType.Google => GPOWrapper.GetAllowedAdvancedPasteGoogleValue(),
                    _ => GpoRuleConfigured.Unavailable,
                };

                return individualGpoRule != GpoRuleConfigured.Disabled;
            }

            var localGpoRule = serviceType switch
            {
                AIServiceType.Ollama => GPOWrapper.GetAllowedAdvancedPasteOllamaValue(),
                AIServiceType.FoundryLocal => GPOWrapper.GetAllowedAdvancedPasteFoundryLocalValue(),
                _ => GpoRuleConfigured.Unavailable,
            };

            return localGpoRule != GpoRuleConfigured.Disabled;
        }

        internal string GetApiKey(string providerId, string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            providerId ??= string.Empty;
            return AICredentialVault.LoadKey(serviceType.ToAIServiceType(), providerId);
        }

        internal string GetProviderEndpoint(string providerId, string serviceType)
        {
            providerId ??= string.Empty;
            var providers = AIConfiguration?.Providers;
            if (providers is null)
            {
                return string.Empty;
            }

            var provider = providers.FirstOrDefault(p => string.Equals(p.Id ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase));
            if (provider is null && !string.IsNullOrWhiteSpace(serviceType))
            {
                provider = providers.FirstOrDefault(p => string.Equals(p.ServiceType, serviceType, StringComparison.OrdinalIgnoreCase));
            }

            return provider?.EndpointUrl?.Trim() ?? string.Empty;
        }

        internal void SaveApiKey(string providerId, string serviceType, string apiKey)
        {
            try
            {
                apiKey = apiKey?.Trim() ?? string.Empty;
                serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
                providerId ??= string.Empty;

                var serviceKind = serviceType.ToAIServiceType();
                if (AICredentialVault.RequiresApiKey(serviceKind) || !string.IsNullOrWhiteSpace(apiKey))
                {
                    AICredentialVault.SaveKey(serviceKind, providerId, apiKey);
                }

                // Also clear any leftover endpoint credential under the legacy Advanced Paste username.
                TryRemoveLegacyEndpointCredential(providerId, serviceType);
            }
            catch (Exception)
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UnsubscribeFromConfiguration(_aiProvidersSettings?.Properties?.AIConfiguration);
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private void InitializeGpoState()
        {
            _onlineAIModelsGpoRuleConfiguration = GPOWrapper.GetAllowedAdvancedPasteOnlineAIModelsValue();
            _onlineAIModelsDisallowedByGPO = _onlineAIModelsGpoRuleConfiguration == GpoRuleConfigured.Disabled;
        }

        private void InitializeProviderState()
        {
            var config = _aiProvidersSettings.Properties.AIConfiguration;
            config.Providers ??= new ObservableCollection<AIProviderDefinition>();
            SyncProviderActiveFlags(config);
            SubscribeToConfiguration(config);
        }

        private static AIServiceType NormalizeServiceType(string serviceType, out string persistedServiceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                persistedServiceType = AIServiceType.OpenAI.ToConfigurationString();
                return AIServiceType.OpenAI;
            }

            var trimmed = serviceType.Trim();
            var serviceTypeKind = trimmed.ToAIServiceType();

            if (serviceTypeKind == AIServiceType.Unknown)
            {
                persistedServiceType = AIServiceType.OpenAI.ToConfigurationString();
                return AIServiceType.OpenAI;
            }

            persistedServiceType = trimmed;
            return serviceTypeKind;
        }

        private static void UpdateProviderFromDraft(AIProviderDefinition target, AIProviderDefinition source)
        {
            if (target is null || source is null)
            {
                return;
            }

            target.ServiceType = source.ServiceType;
            target.ModelName = source.ModelName;
            target.EndpointUrl = source.EndpointUrl;
            target.ApiVersion = source.ApiVersion;
            target.DeploymentName = source.DeploymentName;
            target.ModelPath = source.ModelPath;
            target.SystemPrompt = source.SystemPrompt;
            target.ModerationEnabled = source.ModerationEnabled;
            target.EnableAdvancedAI = source.EnableAdvancedAI;
            target.IsLocalModel = source.IsLocalModel;
        }

        private void RemoveCredentials(string providerId, string serviceType)
        {
            try
            {
                serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
                providerId ??= string.Empty;
                AICredentialVault.SaveKey(serviceType.ToAIServiceType(), providerId, string.Empty);
                TryRemoveLegacyEndpointCredential(providerId, serviceType);
            }
            catch (Exception)
            {
            }
        }

        private static void TryRemoveLegacyEndpointCredential(string providerId, string serviceType)
        {
            try
            {
                var entry = AICredentialVault.BuildCredentialEntry(serviceType.ToAIServiceType(), providerId, legacy: true);
                if (entry is null)
                {
                    return;
                }

                var vault = new PasswordVault();
                try
                {
                    var existing = vault.Retrieve(entry.Value.Resource, entry.Value.Username + "_Endpoint");
                    if (existing is not null)
                    {
                        vault.Remove(existing);
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        private void SaveAndNotifySettings()
        {
            if (_suppressSave)
            {
                return;
            }

            _aiProvidersSettings.Save(_settingsUtils);
            MirrorProvidersIntoAdvancedPasteSettings();
            NotifySettingsChanged();
        }

        private void MirrorProvidersIntoAdvancedPasteSettings()
        {
            try
            {
                var advancedPasteSettings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteSettings.ModuleName);
                if (advancedPasteSettings?.Properties is null)
                {
                    return;
                }

                advancedPasteSettings.Properties.PasteAIConfiguration = AIConfiguration;
                _settingsUtils.SaveSettings(advancedPasteSettings.ToJsonString(), AdvancedPasteSettings.ModuleName);

                // Notify Advanced Paste consumers so they reload mirrored provider config.
                _sendConfigMSG(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                        AdvancedPasteSettings.ModuleName,
                        JsonSerializer.Serialize(advancedPasteSettings, SourceGenerationContextContext.Default.AdvancedPasteSettings)));
            }
            catch
            {
            }
        }

        private void NotifySettingsChanged()
        {
            _sendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    AIProvidersSettings.ModuleName,
                    JsonSerializer.Serialize(_aiProvidersSettings, SourceGenerationContextContext.Default.AIProvidersSettings)));
        }

        private void SubscribeToConfiguration(AIConfiguration configuration)
        {
            if (configuration is null)
            {
                return;
            }

            configuration.PropertyChanged += OnConfigurationPropertyChanged;
            SubscribeToProviders(configuration);
        }

        private void UnsubscribeFromConfiguration(AIConfiguration configuration)
        {
            if (configuration is null)
            {
                return;
            }

            configuration.PropertyChanged -= OnConfigurationPropertyChanged;
            UnsubscribeFromProviders(configuration);
        }

        private void SubscribeToProviders(AIConfiguration configuration)
        {
            if (configuration?.Providers is null)
            {
                return;
            }

            configuration.Providers.CollectionChanged -= OnProvidersCollectionChanged;
            configuration.Providers.CollectionChanged += OnProvidersCollectionChanged;

            foreach (var provider in configuration.Providers)
            {
                provider.PropertyChanged -= OnProviderPropertyChanged;
                provider.PropertyChanged += OnProviderPropertyChanged;
            }
        }

        private void UnsubscribeFromProviders(AIConfiguration configuration)
        {
            if (configuration?.Providers is null)
            {
                return;
            }

            configuration.Providers.CollectionChanged -= OnProvidersCollectionChanged;

            foreach (var provider in configuration.Providers)
            {
                provider.PropertyChanged -= OnProviderPropertyChanged;
            }
        }

        private void OnProvidersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e?.NewItems is not null)
            {
                foreach (AIProviderDefinition provider in e.NewItems)
                {
                    provider.PropertyChanged += OnProviderPropertyChanged;
                }
            }

            if (e?.OldItems is not null)
            {
                foreach (AIProviderDefinition provider in e.OldItems)
                {
                    provider.PropertyChanged -= OnProviderPropertyChanged;
                }
            }

            OnPropertyChanged(nameof(AIConfiguration));
            SaveAndNotifySettings();
        }

        private void OnProviderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(AIProviderDefinition.IsActive), StringComparison.Ordinal))
            {
                return;
            }

            SaveAndNotifySettings();
        }

        private void OnConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(AIConfiguration.Providers), StringComparison.Ordinal))
            {
                SubscribeToProviders(AIConfiguration);
                SyncProviderActiveFlags(AIConfiguration);
                SaveAndNotifySettings();
                return;
            }

            if (string.Equals(e.PropertyName, nameof(AIConfiguration.ActiveProviderId), StringComparison.Ordinal))
            {
                SyncProviderActiveFlags(AIConfiguration);
                SaveAndNotifySettings();
            }
        }

        private static void SyncProviderActiveFlags(AIConfiguration config)
        {
            if (config?.Providers is null)
            {
                return;
            }

            var activeId = config.ActiveProviderId;

            if (string.IsNullOrEmpty(activeId) && config.Providers.Count > 0)
            {
                activeId = config.Providers[0].Id;
                config.ActiveProviderId = activeId;
            }

            foreach (var provider in config.Providers)
            {
                provider.IsActive = !string.IsNullOrEmpty(activeId)
                    && string.Equals(provider.Id, activeId, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
