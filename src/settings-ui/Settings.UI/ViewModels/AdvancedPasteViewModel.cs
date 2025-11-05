// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AdvancedPasteViewModel : PageViewModelBase
    {
        private static readonly HashSet<string> WarnHotkeys = ["Ctrl + V", "Ctrl + Shift + V"];

        private bool _disposed;
        private PasteAIProviderDefinition _pasteAIProviderDraft;
        private PasteAIProviderDefinition _editingPasteAIProvider;

        protected override string ModuleName => AdvancedPasteSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private readonly AdvancedPasteSettings _advancedPasteSettings;
        private readonly AdvancedPasteAdditionalActions _additionalActions;
        private readonly ObservableCollection<AdvancedPasteCustomAction> _customActions;
        private readonly DispatcherQueue _dispatcherQueue;
        private IFileSystemWatcher _settingsWatcher;
        private bool _suppressSave;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private GpoRuleConfigured _onlineAIModelsGpoRuleConfiguration;
        private bool _onlineAIModelsDisallowedByGPO;
        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public AdvancedPasteViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<AdvancedPasteSettings> advancedPasteSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // To obtain the settings configurations of Fancy zones.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            ArgumentNullException.ThrowIfNull(advancedPasteSettingsRepository);

            _advancedPasteSettings = advancedPasteSettingsRepository.SettingsConfig;

            AttachConfigurationHandlers();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _additionalActions = _advancedPasteSettings.Properties.AdditionalActions;
            _customActions = _advancedPasteSettings.Properties.CustomActions.Value;

            SetupSettingsFileWatcher();

            InitializePasteAIProviderState();

            InitializeEnabledValue();
            MigrateLegacyAIEnablement();

            foreach (var action in _additionalActions.GetAllActions())
            {
                action.PropertyChanged += OnAdditionalActionPropertyChanged;
            }

            foreach (var customAction in _customActions)
            {
                customAction.PropertyChanged += OnCustomActionPropertyChanged;
            }

            _customActions.CollectionChanged += OnCustomActionsCollectionChanged;
            UpdateCustomActionsCanMoveUpDown();
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeySettings = new List<HotkeySettings>
            {
                PasteAsPlainTextShortcut,
                AdvancedPasteUIShortcut,
                PasteAsMarkdownShortcut,
                PasteAsJsonShortcut,
            };

            foreach (var action in _additionalActions.GetAllActions())
            {
                if (action is AdvancedPasteAdditionalAction additionalAction)
                {
                    hotkeySettings.Add(additionalAction.Shortcut);
                }
            }

            // Custom actions do not have localization header, just use the action name.
            foreach (var customAction in _customActions)
            {
                hotkeySettings.Add(customAction.Shortcut);
            }

            return new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = hotkeySettings.ToArray(),
            };
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredAdvancedPasteEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.AdvancedPaste;
            }

            _onlineAIModelsGpoRuleConfiguration = GPOWrapper.GetAllowedAdvancedPasteOnlineAIModelsValue();
            _onlineAIModelsDisallowedByGPO = _onlineAIModelsGpoRuleConfiguration == GpoRuleConfigured.Disabled;

            if (_onlineAIModelsDisallowedByGPO)
            {
                // disable AI if it was enabled
                DisableAI();
            }
        }

        private void MigrateLegacyAIEnablement()
        {
            if (_advancedPasteSettings.Properties.IsAIEnabled || IsOnlineAIModelsDisallowedByGPO)
            {
                return;
            }

            if (!LegacyOpenAIKeyExists())
            {
                return;
            }

            _advancedPasteSettings.Properties.IsAIEnabled = true;
            SaveAndNotifySettings();
            OnPropertyChanged(nameof(IsAIEnabled));
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(ShowOnlineAIModelsGpoConfiguredInfoBar));
                    OnPropertyChanged(nameof(ShowClipboardHistoryIsGpoConfiguredInfoBar));

                    // Set the status of AdvancedPaste in the general settings
                    GeneralSettingsConfig.Enabled.AdvancedPaste = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public ObservableCollection<AdvancedPasteCustomAction> CustomActions => _customActions;

        public AdvancedPasteAdditionalActions AdditionalActions => _additionalActions;

        public static IEnumerable<AIServiceTypeMetadata> AvailableProviders => AIServiceTypeRegistry.GetAvailableServiceTypes();

        /// <summary>
        /// Gets available AI providers filtered by GPO policies.
        /// Only returns providers that are not explicitly disabled by GPO.
        /// </summary>
        public IEnumerable<AIServiceTypeMetadata> AvailableProvidersFilteredByGPO =>
            AvailableProviders.Where(metadata => IsServiceTypeAllowedByGPO(metadata.ServiceType));

        public bool IsAIEnabled => _advancedPasteSettings.Properties.IsAIEnabled && !IsOnlineAIModelsDisallowedByGPO;

        private bool LegacyOpenAIKeyExists()
        {
            try
            {
                PasswordVault vault = new();

                // return vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey") is not null;
                var legacyOpenAIKey = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                if (legacyOpenAIKey != null)
                {
                    string credentialResource = GetAICredentialResource("OpenAI");
                    var targetProvider = PasteAIConfiguration?.ActiveProvider ?? PasteAIConfiguration?.Providers?.FirstOrDefault();
                    string providerId = targetProvider?.Id ?? string.Empty;
                    string serviceType = targetProvider?.ServiceType ?? "OpenAI";
                    string credentialUserName = GetPasteAICredentialUserName(providerId, serviceType);
                    PasswordCredential cred = new(credentialResource, credentialUserName, legacyOpenAIKey.Password);
                    vault.Add(cred);

                    // delete old key
                    TryRemoveCredential(vault, "https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool IsOnlineAIModelsDisallowedByGPO
        {
            get => _onlineAIModelsDisallowedByGPO || _enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
        }

        public bool ShowOnlineAIModelsGpoConfiguredInfoBar
        {
            get => _onlineAIModelsDisallowedByGPO && _isEnabled;
        }

        private bool IsClipboardHistoryEnabled()
        {
            string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Clipboard\";
            try
            {
                int enableClipboardHistory = (int)Registry.GetValue(registryKey, "EnableClipboardHistory", false);
                return enableClipboardHistory != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsClipboardHistoryDisabledByGPO()
        {
            string registryKey = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\";
            try
            {
                object allowClipboardHistory = Registry.GetValue(registryKey, "AllowClipboardHistory", null);
                if (allowClipboardHistory != null)
                {
                    return (int)allowClipboardHistory == 0;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void SetClipboardHistoryEnabled(bool value)
        {
            string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Clipboard\";
            try
            {
                Registry.SetValue(registryKey, "EnableClipboardHistory", value ? 1 : 0);
            }
            catch (Exception)
            {
            }
        }

        public bool ClipboardHistoryEnabled
        {
            get => IsClipboardHistoryEnabled();
            set
            {
                if (IsClipboardHistoryEnabled() != value)
                {
                    SetClipboardHistoryEnabled(value);
                }
            }
        }

        public bool ClipboardHistoryDisabledByGPO
        {
            get => IsClipboardHistoryDisabledByGPO();
        }

        public bool ShowClipboardHistoryIsGpoConfiguredInfoBar
        {
            get => IsClipboardHistoryDisabledByGPO() && _isEnabled;
        }

        public HotkeySettings AdvancedPasteUIShortcut
        {
            get => _advancedPasteSettings.Properties.AdvancedPasteUIShortcut;
            set
            {
                if (_advancedPasteSettings.Properties.AdvancedPasteUIShortcut != value)
                {
                    _advancedPasteSettings.Properties.AdvancedPasteUIShortcut = value ?? AdvancedPasteProperties.DefaultAdvancedPasteUIShortcut;
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));
                    OnPropertyChanged(nameof(AdvancedPasteUIShortcut));
                    SaveAndNotifySettings();
                }
            }
        }

        public HotkeySettings PasteAsPlainTextShortcut
        {
            get => _advancedPasteSettings.Properties.PasteAsPlainTextShortcut;
            set
            {
                if (_advancedPasteSettings.Properties.PasteAsPlainTextShortcut != value)
                {
                    _advancedPasteSettings.Properties.PasteAsPlainTextShortcut = value ?? AdvancedPasteProperties.DefaultPasteAsPlainTextShortcut;
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));
                    OnPropertyChanged(nameof(PasteAsPlainTextShortcut));
                    SaveAndNotifySettings();
                }
            }
        }

        public HotkeySettings PasteAsMarkdownShortcut
        {
            get => _advancedPasteSettings.Properties.PasteAsMarkdownShortcut;
            set
            {
                if (_advancedPasteSettings.Properties.PasteAsMarkdownShortcut != value)
                {
                    _advancedPasteSettings.Properties.PasteAsMarkdownShortcut = value ?? new HotkeySettings();
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));
                    OnPropertyChanged(nameof(PasteAsMarkdownShortcut));
                    SaveAndNotifySettings();
                }
            }
        }

        public HotkeySettings PasteAsJsonShortcut
        {
            get => _advancedPasteSettings.Properties.PasteAsJsonShortcut;
            set
            {
                if (_advancedPasteSettings.Properties.PasteAsJsonShortcut != value)
                {
                    _advancedPasteSettings.Properties.PasteAsJsonShortcut = value ?? new HotkeySettings();
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));
                    OnPropertyChanged(nameof(PasteAsJsonShortcut));
                    SaveAndNotifySettings();
                }
            }
        }

        public PasteAIConfiguration PasteAIConfiguration
        {
            get => _advancedPasteSettings.Properties.PasteAIConfiguration;
            set
            {
                if (!ReferenceEquals(value, _advancedPasteSettings.Properties.PasteAIConfiguration))
                {
                    UnsubscribeFromPasteAIConfiguration(_advancedPasteSettings.Properties.PasteAIConfiguration);

                    var newValue = value ?? new PasteAIConfiguration();
                    _advancedPasteSettings.Properties.PasteAIConfiguration = newValue;
                    SubscribeToPasteAIConfiguration(newValue);

                    OnPropertyChanged(nameof(PasteAIConfiguration));
                    SaveAndNotifySettings();
                }
            }
        }

        public PasteAIProviderDefinition PasteAIProviderDraft
        {
            get => _pasteAIProviderDraft;
            private set
            {
                if (!ReferenceEquals(_pasteAIProviderDraft, value))
                {
                    _pasteAIProviderDraft = value;
                    OnPropertyChanged(nameof(PasteAIProviderDraft));
                    OnPropertyChanged(nameof(ShowPasteAIProviderGpoConfiguredInfoBar));
                }
            }
        }

        public bool ShowPasteAIProviderGpoConfiguredInfoBar
        {
            get
            {
                if (_pasteAIProviderDraft is null)
                {
                    return false;
                }

                var serviceType = _pasteAIProviderDraft.ServiceType.ToAIServiceType();
                return !IsServiceTypeAllowedByGPO(serviceType);
            }
        }

        public bool IsEditingPasteAIProvider => _editingPasteAIProvider is not null;

        public bool ShowCustomPreview
        {
            get => _advancedPasteSettings.Properties.ShowCustomPreview;
            set
            {
                if (value != _advancedPasteSettings.Properties.ShowCustomPreview)
                {
                    _advancedPasteSettings.Properties.ShowCustomPreview = value;
                    NotifySettingsChanged();
                }
            }
        }

        public bool CloseAfterLosingFocus
        {
            get => _advancedPasteSettings.Properties.CloseAfterLosingFocus;
            set
            {
                if (value != _advancedPasteSettings.Properties.CloseAfterLosingFocus)
                {
                    _advancedPasteSettings.Properties.CloseAfterLosingFocus = value;
                    NotifySettingsChanged();
                }
            }
        }

        public bool IsConflictingCopyShortcut =>
            _customActions.Select(customAction => customAction.Shortcut)
                          .Concat([PasteAsPlainTextShortcut, AdvancedPasteUIShortcut, PasteAsMarkdownShortcut, PasteAsJsonShortcut])
                          .Any(hotkey => WarnHotkeys.Contains(hotkey.ToString()));

        public bool IsAdditionalActionConflictingCopyShortcut =>
            _additionalActions.GetAllActions()
                              .OfType<AdvancedPasteAdditionalAction>()
                              .Select(additionalAction => additionalAction.Shortcut)
                              .Any(hotkey => WarnHotkeys.Contains(hotkey.ToString()));

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    AdvancedPasteSettings.ModuleName,
                    JsonSerializer.Serialize(_advancedPasteSettings, SourceGenerationContextContext.Default.AdvancedPasteSettings)));
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            MigrateLegacyAIEnablement();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ShowOnlineAIModelsGpoConfiguredInfoBar));
            OnPropertyChanged(nameof(ShowClipboardHistoryIsGpoConfiguredInfoBar));
            OnPropertyChanged(nameof(IsAIEnabled));
        }

        public void BeginAddPasteAIProvider(string serviceType)
        {
            var normalizedServiceType = NormalizeServiceType(serviceType, out var persistedServiceType);

            var metadata = AIServiceTypeRegistry.GetMetadata(normalizedServiceType);
            var provider = new PasteAIProviderDefinition
            {
                ServiceType = persistedServiceType,
                ModelName = GetDefaultModelName(normalizedServiceType),
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

            _editingPasteAIProvider = null;
            PasteAIProviderDraft = provider;
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

        private static string GetDefaultModelName(AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "gpt-4",
                AIServiceType.AzureOpenAI => "gpt-4",
                AIServiceType.Mistral => "mistral-large-latest",
                AIServiceType.Google => "gemini-1.5-pro",
                AIServiceType.AzureAIInference => "gpt-4o-mini",
                AIServiceType.Ollama => "llama3",
                AIServiceType.Anthropic => "claude-3-5-sonnet",
                AIServiceType.AmazonBedrock => "anthropic.claude-3-haiku",
                _ => string.Empty,
            };
        }

        public bool IsServiceTypeAllowedByGPO(AIServiceType serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);

            // Check if this is an online service
            if (metadata.IsOnlineService)
            {
                // For online services, first check the global online AI models GPO
                if (_onlineAIModelsGpoRuleConfiguration == GpoRuleConfigured.Disabled)
                {
                    // If global online AI is disabled, all online services are blocked
                    return false;
                }

                // If global online AI is enabled or not configured, check individual endpoint GPO
                var individualGpoRule = serviceType switch
                {
                    AIServiceType.OpenAI => GPOWrapper.GetAllowedAdvancedPasteOpenAIValue(),
                    AIServiceType.AzureOpenAI => GPOWrapper.GetAllowedAdvancedPasteAzureOpenAIValue(),
                    AIServiceType.AzureAIInference => GPOWrapper.GetAllowedAdvancedPasteAzureAIInferenceValue(),
                    AIServiceType.Mistral => GPOWrapper.GetAllowedAdvancedPasteMistralValue(),
                    AIServiceType.Google => GPOWrapper.GetAllowedAdvancedPasteGoogleValue(),
                    AIServiceType.Anthropic => GPOWrapper.GetAllowedAdvancedPasteAnthropicValue(),
                    _ => GpoRuleConfigured.Unavailable,
                };

                // If individual GPO is explicitly disabled, block it
                return individualGpoRule != GpoRuleConfigured.Disabled;
            }
            else
            {
                // For local models, only check their individual GPO (not affected by online AI GPO)
                var localGpoRule = serviceType switch
                {
                    AIServiceType.Ollama => GPOWrapper.GetAllowedAdvancedPasteOllamaValue(),
                    AIServiceType.FoundryLocal => GPOWrapper.GetAllowedAdvancedPasteFoundryLocalValue(),
                    _ => GpoRuleConfigured.Unavailable,
                };

                // If local model GPO is explicitly disabled, block it
                return localGpoRule != GpoRuleConfigured.Disabled;
            }
        }

        public void BeginEditPasteAIProvider(PasteAIProviderDefinition provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            _editingPasteAIProvider = provider;
            var draft = provider.Clone();
            var storedEndpoint = GetPasteAIEndpoint(draft.Id, draft.ServiceType);
            if (!string.IsNullOrWhiteSpace(storedEndpoint))
            {
                draft.EndpointUrl = storedEndpoint;
            }

            PasteAIProviderDraft = draft;
        }

        public void CancelPasteAIProviderDraft()
        {
            PasteAIProviderDraft = null;
            _editingPasteAIProvider = null;
        }

        public void CommitPasteAIProviderDraft(string apiKey, string endpoint)
        {
            if (PasteAIProviderDraft is null)
            {
                return;
            }

            var config = PasteAIConfiguration ?? new PasteAIConfiguration();
            if (_advancedPasteSettings.Properties.PasteAIConfiguration is null)
            {
                PasteAIConfiguration = config;
            }

            var draft = PasteAIProviderDraft;
            draft.EndpointUrl = endpoint?.Trim() ?? string.Empty;

            SavePasteAIApiKey(draft.Id, draft.ServiceType, apiKey);

            if (_editingPasteAIProvider is null)
            {
                config.Providers.Add(draft);
                config.ActiveProviderId ??= draft.Id;
            }
            else
            {
                UpdateProviderFromDraft(_editingPasteAIProvider, draft);
                _editingPasteAIProvider = null;
            }

            PasteAIProviderDraft = null;
            SaveAndNotifySettings();
            OnPropertyChanged(nameof(PasteAIConfiguration));
        }

        public void RemovePasteAIProvider(PasteAIProviderDefinition provider)
        {
            if (provider is null)
            {
                return;
            }

            var config = PasteAIConfiguration;
            if (config?.Providers is null)
            {
                return;
            }

            if (config.Providers.Remove(provider))
            {
                RemovePasteAICredentials(provider.Id, provider.ServiceType);
                SaveAndNotifySettings();
                OnPropertyChanged(nameof(PasteAIConfiguration));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UnsubscribeFromPasteAIConfiguration(_advancedPasteSettings?.Properties.PasteAIConfiguration);

                    foreach (var action in _additionalActions.GetAllActions())
                    {
                        action.PropertyChanged -= OnAdditionalActionPropertyChanged;
                    }

                    foreach (var customAction in _customActions)
                    {
                        customAction.PropertyChanged -= OnCustomActionPropertyChanged;
                    }

                    _customActions.CollectionChanged -= OnCustomActionsCollectionChanged;
                    _settingsWatcher?.Dispose();
                    _settingsWatcher = null;
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        internal void DisableAI()
        {
            try
            {
                bool stateChanged = false;

                if (_advancedPasteSettings.Properties.IsAIEnabled)
                {
                    _advancedPasteSettings.Properties.IsAIEnabled = false;
                    stateChanged = true;
                }

                if (stateChanged)
                {
                    SaveAndNotifySettings();
                }
                else
                {
                    NotifySettingsChanged();
                }

                OnPropertyChanged(nameof(IsAIEnabled));
            }
            catch (Exception)
            {
            }
        }

        internal void EnableAI()
        {
            try
            {
                if (IsOnlineAIModelsDisallowedByGPO)
                {
                    return;
                }

                bool stateChanged = false;

                if (!_advancedPasteSettings.Properties.IsAIEnabled)
                {
                    _advancedPasteSettings.Properties.IsAIEnabled = true;
                    stateChanged = true;
                }

                if (stateChanged)
                {
                    SaveAndNotifySettings();
                }
                else
                {
                    NotifySettingsChanged();
                }

                OnPropertyChanged(nameof(IsAIEnabled));
            }
            catch (Exception)
            {
            }
        }

        internal void SavePasteAIApiKey(string providerId, string serviceType, string apiKey)
        {
            try
            {
                apiKey = apiKey?.Trim() ?? string.Empty;
                serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
                providerId ??= string.Empty;

                string credentialResource = GetAICredentialResource(serviceType);
                string credentialUserName = GetPasteAICredentialUserName(providerId, serviceType);
                string endpointCredentialUserName = GetPasteAIEndpointCredentialUserName(providerId, serviceType);
                PasswordVault vault = new();
                TryRemoveCredential(vault, credentialResource, credentialUserName);
                TryRemoveCredential(vault, credentialResource, endpointCredentialUserName);

                bool storeApiKey = RequiresCredentialStorage(serviceType) && !string.IsNullOrWhiteSpace(apiKey);
                if (storeApiKey)
                {
                    PasswordCredential cred = new(credentialResource, credentialUserName, apiKey);
                    vault.Add(cred);
                }

                OnPropertyChanged(nameof(IsAIEnabled));
                NotifySettingsChanged();
            }
            catch (Exception)
            {
            }
        }

        internal string GetPasteAIApiKey(string providerId, string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            providerId ??= string.Empty;
            return RetrieveCredentialValue(
                GetAICredentialResource(serviceType),
                GetPasteAICredentialUserName(providerId, serviceType));
        }

        internal string GetPasteAIEndpoint(string providerId, string serviceType)
        {
            providerId ??= string.Empty;
            var providers = PasteAIConfiguration?.Providers;
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

        private string GetAICredentialResource(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return serviceType.ToLowerInvariant() switch
            {
                "openai" => "https://platform.openai.com/api-keys",
                "azureopenai" => "https://azure.microsoft.com/products/ai-services/openai-service",
                "azureaiinference" => "https://azure.microsoft.com/products/ai-services/ai-inference",
                "mistral" => "https://console.mistral.ai/account/api-keys",
                "google" => "https://ai.google.dev/",
                "huggingface" => "https://huggingface.co/settings/tokens",
                "anthropic" => "https://console.anthropic.com/account/keys",
                "amazonbedrock" => "https://aws.amazon.com/bedrock/",
                "ollama" => "https://ollama.com/",
                _ => "https://platform.openai.com/api-keys",
            };
        }

        private string GetPasteAICredentialUserName(string providerId, string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            providerId ??= string.Empty;

            string service = serviceType.ToLowerInvariant();
            string normalizedId = NormalizeProviderIdentifier(providerId);

            return $"PowerToys_AdvancedPaste_PasteAI_{service}_{normalizedId}";
        }

        private string GetPasteAIEndpointCredentialUserName(string providerId, string serviceType)
        {
            return GetPasteAICredentialUserName(providerId, serviceType) + "_Endpoint";
        }

        private static void UpdateProviderFromDraft(PasteAIProviderDefinition target, PasteAIProviderDefinition source)
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

        private void RemovePasteAICredentials(string providerId, string serviceType)
        {
            try
            {
                serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
                providerId ??= string.Empty;

                string credentialResource = GetAICredentialResource(serviceType);
                PasswordVault vault = new();
                TryRemoveCredential(vault, credentialResource, GetPasteAICredentialUserName(providerId, serviceType));
                TryRemoveCredential(vault, credentialResource, GetPasteAIEndpointCredentialUserName(providerId, serviceType));
            }
            catch (Exception)
            {
            }
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

        private static bool RequiresCredentialStorage(string serviceType)
        {
            var serviceTypeKind = serviceType.ToAIServiceType();

            return serviceTypeKind switch
            {
                AIServiceType.Onnx => false,
                AIServiceType.Ollama => false,
                AIServiceType.FoundryLocal => false,
                AIServiceType.ML => false,
                _ => true,
            };
        }

        private static void TryRemoveCredential(PasswordVault vault, string credentialResource, string credentialUserName)
        {
            try
            {
                PasswordCredential existingCred = vault.Retrieve(credentialResource, credentialUserName);
                vault.Remove(existingCred);
            }
            catch (Exception)
            {
                // Credential doesn't exist, which is fine
            }
        }

        internal AdvancedPasteCustomAction GetNewCustomAction(string namePrefix)
        {
            ArgumentException.ThrowIfNullOrEmpty(namePrefix);

            var maxUsedPrefix = _customActions.Select(customAction => customAction.Name)
                                  .Where(name => name.StartsWith(namePrefix, StringComparison.InvariantCulture))
                                  .Select(name => int.TryParse(name.AsSpan(namePrefix.Length), out int number) ? number : 0)
                                  .DefaultIfEmpty(0)
                                  .Max();

            var maxUsedId = _customActions.Select(customAction => customAction.Id)
                                          .DefaultIfEmpty(-1)
                                          .Max();
            return new()
            {
                Id = maxUsedId + 1,
                Name = $"{namePrefix} {maxUsedPrefix + 1}",
                IsShown = true,
            };
        }

        internal void AddCustomAction(AdvancedPasteCustomAction customAction)
        {
            if (_customActions.Any(existingCustomAction => existingCustomAction.Id == customAction.Id))
            {
                throw new ArgumentException("Duplicate custom action", nameof(customAction));
            }

            _customActions.Add(customAction);
        }

        internal void DeleteCustomAction(AdvancedPasteCustomAction customAction) => _customActions.Remove(customAction);

        private void SaveCustomActions() => SaveAndNotifySettings();

        private void SaveAndNotifySettings()
        {
            if (_suppressSave)
            {
                return;
            }

            _settingsUtils.SaveSettings(_advancedPasteSettings.ToJsonString(), AdvancedPasteSettings.ModuleName);
            NotifySettingsChanged();
        }

        private void OnAdditionalActionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SaveAndNotifySettings();

            if (e.PropertyName == nameof(AdvancedPasteAdditionalAction.Shortcut))
            {
                OnPropertyChanged(nameof(IsAdditionalActionConflictingCopyShortcut));
            }
        }

        private void OnCustomActionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (typeof(AdvancedPasteCustomAction).GetProperty(e.PropertyName).GetCustomAttribute<JsonIgnoreAttribute>() == null)
            {
                SaveCustomActions();
            }

            if (e.PropertyName == nameof(AdvancedPasteCustomAction.Shortcut))
            {
                OnPropertyChanged(nameof(IsConflictingCopyShortcut));
            }
        }

        private void OnCustomActionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            void AddRange(System.Collections.IList items)
            {
                foreach (AdvancedPasteCustomAction item in items)
                {
                    item.PropertyChanged += OnCustomActionPropertyChanged;
                }
            }

            void RemoveRange(System.Collections.IList items)
            {
                foreach (AdvancedPasteCustomAction item in items)
                {
                    item.PropertyChanged -= OnCustomActionPropertyChanged;
                }
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddRange(e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveRange(e.OldItems);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    AddRange(e.NewItems);
                    RemoveRange(e.OldItems);
                    break;

                case NotifyCollectionChangedAction.Move:
                    break;

                default:
                    throw new ArgumentException($"Unsupported {nameof(e.Action)} {e.Action}", nameof(e));
            }

            OnPropertyChanged(nameof(IsConflictingCopyShortcut));
            UpdateCustomActionsCanMoveUpDown();
            SaveCustomActions();
        }

        private void AttachConfigurationHandlers()
        {
            SubscribeToPasteAIConfiguration(_advancedPasteSettings.Properties.PasteAIConfiguration);
        }

        private void SetupSettingsFileWatcher()
        {
            _settingsWatcher = Helper.GetFileWatcher(AdvancedPasteSettings.ModuleName, SettingsUtils.DefaultFileName, OnSettingsFileChanged);
        }

        private void OnSettingsFileChanged()
        {
            if (_disposed)
            {
                return;
            }

            void Handler()
            {
                ApplyExternalSettings();
            }

            if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
            {
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, Handler);
            }
            else
            {
                Handler();
            }
        }

        private void ApplyExternalSettings()
        {
            if (_disposed)
            {
                return;
            }

            AdvancedPasteSettings latestSettings;

            try
            {
                latestSettings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteSettings.ModuleName);
            }
            catch
            {
                return;
            }

            if (latestSettings?.Properties is null)
            {
                return;
            }

            try
            {
                _suppressSave = true;
                ApplyExternalProperties(latestSettings.Properties);
            }
            finally
            {
                _suppressSave = false;
            }
        }

        private void ApplyExternalProperties(AdvancedPasteProperties source)
        {
            var target = _advancedPasteSettings?.Properties;

            if (target is null || source is null)
            {
                return;
            }

            if (target.IsAIEnabled != source.IsAIEnabled)
            {
                target.IsAIEnabled = source.IsAIEnabled;
                OnPropertyChanged(nameof(IsAIEnabled));
            }

            if (target.ShowCustomPreview != source.ShowCustomPreview)
            {
                target.ShowCustomPreview = source.ShowCustomPreview;
                OnPropertyChanged(nameof(ShowCustomPreview));
            }

            if (target.CloseAfterLosingFocus != source.CloseAfterLosingFocus)
            {
                target.CloseAfterLosingFocus = source.CloseAfterLosingFocus;
                OnPropertyChanged(nameof(CloseAfterLosingFocus));
            }

            var incomingConfig = source.PasteAIConfiguration ?? new PasteAIConfiguration();
            if (ShouldReplacePasteAIConfiguration(target.PasteAIConfiguration, incomingConfig))
            {
                PasteAIConfiguration = incomingConfig;
            }
        }

        private static bool ShouldReplacePasteAIConfiguration(PasteAIConfiguration current, PasteAIConfiguration incoming)
        {
            if (incoming is null)
            {
                return false;
            }

            if (current is null)
            {
                return true;
            }

            if (!string.Equals(current.ActiveProviderId ?? string.Empty, incoming.ActiveProviderId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.UseSharedCredentials != incoming.UseSharedCredentials)
            {
                return true;
            }

            var currentProviders = current.Providers ?? new ObservableCollection<PasteAIProviderDefinition>();
            var incomingProviders = incoming.Providers ?? new ObservableCollection<PasteAIProviderDefinition>();

            if (currentProviders.Count != incomingProviders.Count)
            {
                return true;
            }

            for (int i = 0; i < currentProviders.Count; i++)
            {
                var existing = currentProviders[i];
                var updated = incomingProviders[i];

                if (!string.Equals(existing?.Id ?? string.Empty, updated?.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.Equals(existing?.ServiceType ?? string.Empty, updated?.ServiceType ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.Equals(existing?.ModelName ?? string.Empty, updated?.ModelName ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.Equals(existing?.EndpointUrl ?? string.Empty, updated?.EndpointUrl ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.Equals(existing?.DeploymentName ?? string.Empty, updated?.DeploymentName ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.Equals(existing?.ApiVersion ?? string.Empty, updated?.ApiVersion ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.Equals(existing?.SystemPrompt ?? string.Empty, updated?.SystemPrompt ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }

                if (existing?.ModerationEnabled != updated?.ModerationEnabled || existing?.EnableAdvancedAI != updated?.EnableAdvancedAI || existing?.IsActive != updated?.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        private void SubscribeToPasteAIConfiguration(PasteAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged += OnPasteAIConfigurationPropertyChanged;
                SubscribeToPasteAIProviders(configuration);
            }
        }

        private void UnsubscribeFromPasteAIConfiguration(PasteAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged -= OnPasteAIConfigurationPropertyChanged;
                UnsubscribeFromPasteAIProviders(configuration);
            }
        }

        private void SubscribeToPasteAIProviders(PasteAIConfiguration configuration)
        {
            if (configuration?.Providers is null)
            {
                return;
            }

            configuration.Providers.CollectionChanged -= OnPasteAIProvidersCollectionChanged;
            configuration.Providers.CollectionChanged += OnPasteAIProvidersCollectionChanged;

            foreach (var provider in configuration.Providers)
            {
                provider.PropertyChanged -= OnPasteAIProviderPropertyChanged;
                provider.PropertyChanged += OnPasteAIProviderPropertyChanged;
            }
        }

        private void UnsubscribeFromPasteAIProviders(PasteAIConfiguration configuration)
        {
            if (configuration?.Providers is null)
            {
                return;
            }

            configuration.Providers.CollectionChanged -= OnPasteAIProvidersCollectionChanged;

            foreach (var provider in configuration.Providers)
            {
                provider.PropertyChanged -= OnPasteAIProviderPropertyChanged;
            }
        }

        private void OnPasteAIProvidersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e?.NewItems is not null)
            {
                foreach (PasteAIProviderDefinition provider in e.NewItems)
                {
                    provider.PropertyChanged += OnPasteAIProviderPropertyChanged;
                }
            }

            if (e?.OldItems is not null)
            {
                foreach (PasteAIProviderDefinition provider in e.OldItems)
                {
                    provider.PropertyChanged -= OnPasteAIProviderPropertyChanged;
                }
            }

            var pasteConfig = _advancedPasteSettings?.Properties?.PasteAIConfiguration;

            OnPropertyChanged(nameof(PasteAIConfiguration));
            SaveAndNotifySettings();
        }

        private void OnPasteAIProviderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is PasteAIProviderDefinition provider)
            {
                // When service type changes we may need to update credentials entry names.
                if (string.Equals(e.PropertyName, nameof(PasteAIProviderDefinition.ServiceType), StringComparison.Ordinal))
                {
                    SaveAndNotifySettings();
                    return;
                }

                SaveAndNotifySettings();
            }
        }

        private void OnPasteAIConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(PasteAIConfiguration.Providers), StringComparison.Ordinal))
            {
                SubscribeToPasteAIProviders(PasteAIConfiguration);
                SaveAndNotifySettings();
                return;
            }

            if (string.Equals(e.PropertyName, nameof(PasteAIConfiguration.ActiveProviderId), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(PasteAIConfiguration.UseSharedCredentials), StringComparison.Ordinal))
            {
                SaveAndNotifySettings();
            }
        }

        private void InitializePasteAIProviderState()
        {
            var pasteConfig = _advancedPasteSettings?.Properties?.PasteAIConfiguration;
            if (pasteConfig is null)
            {
                _advancedPasteSettings.Properties.PasteAIConfiguration = new PasteAIConfiguration();
                pasteConfig = _advancedPasteSettings.Properties.PasteAIConfiguration;
            }

            pasteConfig.Providers ??= new ObservableCollection<PasteAIProviderDefinition>();
            SubscribeToPasteAIProviders(pasteConfig);
        }

        private static string RetrieveCredentialValue(string credentialResource, string credentialUserName)
        {
            if (string.IsNullOrWhiteSpace(credentialResource) || string.IsNullOrWhiteSpace(credentialUserName))
            {
                return string.Empty;
            }

            try
            {
                PasswordVault vault = new();
                PasswordCredential existingCred = vault.Retrieve(credentialResource, credentialUserName);
                existingCred?.RetrievePassword();
                return existingCred?.Password?.Trim() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void UpdateCustomActionsCanMoveUpDown()
        {
            for (int index = 0; index < _customActions.Count; index++)
            {
                var customAction = _customActions[index];
                customAction.CanMoveUp = index != 0;
                customAction.CanMoveDown = index != _customActions.Count - 1;
            }
        }
    }
}
