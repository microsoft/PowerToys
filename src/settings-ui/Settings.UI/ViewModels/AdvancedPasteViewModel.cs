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
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AdvancedPasteViewModel : PageViewModelBase
    {
        private static readonly HashSet<string> WarnHotkeys = ["Ctrl + V", "Ctrl + Shift + V"];
        private static readonly HashSet<string> AdvancedAITrackedProperties = new(StringComparer.Ordinal)
        {
            nameof(AdvancedAIConfiguration.ModelName),
            nameof(AdvancedAIConfiguration.EndpointUrl),
            nameof(AdvancedAIConfiguration.ApiVersion),
            nameof(AdvancedAIConfiguration.DeploymentName),
            nameof(AdvancedAIConfiguration.ModelPath),
            nameof(AdvancedAIConfiguration.SystemPrompt),
            nameof(AdvancedAIConfiguration.ModerationEnabled),
        };

        private static readonly HashSet<string> PasteAITrackedProperties = new(StringComparer.Ordinal)
        {
            nameof(PasteAIConfiguration.ModelName),
            nameof(PasteAIConfiguration.EndpointUrl),
            nameof(PasteAIConfiguration.ApiVersion),
            nameof(PasteAIConfiguration.DeploymentName),
            nameof(PasteAIConfiguration.ModelPath),
            nameof(PasteAIConfiguration.SystemPrompt),
            nameof(PasteAIConfiguration.ModerationEnabled),
        };

        private bool _disposed;
        private bool _isLoadingAdvancedAIProviderConfiguration;
        private bool _isLoadingPasteAIProviderConfiguration;

        protected override string ModuleName => AdvancedPasteSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private readonly AdvancedPasteSettings _advancedPasteSettings;
        private readonly AdvancedPasteAdditionalActions _additionalActions;
        private readonly ObservableCollection<AdvancedPasteCustomAction> _customActions;

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

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            ArgumentNullException.ThrowIfNull(advancedPasteSettingsRepository);

            _advancedPasteSettings = advancedPasteSettingsRepository.SettingsConfig;
            SeedProviderConfigurationSnapshots();

            AttachConfigurationHandlers();

            _additionalActions = _advancedPasteSettings.Properties.AdditionalActions;
            _customActions = _advancedPasteSettings.Properties.CustomActions.Value;

            LoadAdvancedAIProviderConfiguration();
            LoadPasteAIProviderConfiguration();

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

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

        public bool IsAIEnabled => _advancedPasteSettings.Properties.IsAIEnabled;

        public bool IsAISettingEnabled => !IsOnlineAIModelsDisallowedByGPO;

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

        public bool IsAdvancedAIEnabled
        {
            get => _advancedPasteSettings.Properties.IsAdvancedAIEnabled;
            set
            {
                if (value != _advancedPasteSettings.Properties.IsAdvancedAIEnabled)
                {
                    _advancedPasteSettings.Properties.IsAdvancedAIEnabled = value;
                    OnPropertyChanged(nameof(IsAdvancedAIEnabled));
                    NotifySettingsChanged();
                }
            }
        }

        public AdvancedAIConfiguration AdvancedAIConfiguration
        {
            get => _advancedPasteSettings.Properties.AdvancedAIConfiguration;
            set
            {
                if (!ReferenceEquals(value, _advancedPasteSettings.Properties.AdvancedAIConfiguration))
                {
                    UnsubscribeFromAdvancedAIConfiguration(_advancedPasteSettings.Properties.AdvancedAIConfiguration);

                    var newValue = value ?? new AdvancedAIConfiguration();
                    _advancedPasteSettings.Properties.AdvancedAIConfiguration = newValue;
                    SubscribeToAdvancedAIConfiguration(newValue);

                    OnPropertyChanged(nameof(AdvancedAIConfiguration));
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
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ShowOnlineAIModelsGpoConfiguredInfoBar));
            OnPropertyChanged(nameof(ShowClipboardHistoryIsGpoConfiguredInfoBar));
            OnPropertyChanged(nameof(IsAIEnabled));
            OnPropertyChanged(nameof(IsAISettingEnabled));
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UnsubscribeFromAdvancedAIConfiguration(_advancedPasteSettings?.Properties.AdvancedAIConfiguration);
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
                OnPropertyChanged(nameof(IsAISettingEnabled));
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

                if (!_advancedPasteSettings.Properties.IsAdvancedAIEnabled)
                {
                    _advancedPasteSettings.Properties.IsAdvancedAIEnabled = true; // new users should get Semantic Kernel benefits immediately
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
                OnPropertyChanged(nameof(IsAISettingEnabled));
                OnPropertyChanged(nameof(IsAdvancedAIEnabled));
            }
            catch (Exception)
            {
            }
        }

        internal void SaveAdvancedAICredential(string serviceType, string endpoint, string apiKey)
        {
            try
            {
                endpoint = endpoint?.Trim() ?? string.Empty;
                apiKey = apiKey?.Trim() ?? string.Empty;
                string credentialResource = GetAdvancedAICredentialResource(serviceType);
                string credentialUserName = GetAdvancedAICredentialUserName(serviceType);
                string endpointCredentialUserName = GetAdvancedAIEndpointCredentialUserName(serviceType);

                PasswordVault vault = new();
                TryRemoveCredential(vault, credentialResource, credentialUserName);
                TryRemoveCredential(vault, credentialResource, endpointCredentialUserName);

                bool storeApiKey = RequiresCredentialStorage(serviceType) && !string.IsNullOrWhiteSpace(apiKey);
                if (storeApiKey)
                {
                    PasswordCredential cred = new(credentialResource, credentialUserName, apiKey);
                    vault.Add(cred);
                }

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    PasswordCredential endpointCred = new(credentialResource, endpointCredentialUserName, endpoint);
                    vault.Add(endpointCred);
                }

                OnPropertyChanged(nameof(IsAIEnabled));
                NotifySettingsChanged();
            }
            catch (Exception)
            {
            }
        }

        internal void SavePasteAICredential(string serviceType, string endpoint, string apiKey)
        {
            try
            {
                endpoint = endpoint?.Trim() ?? string.Empty;
                apiKey = apiKey?.Trim() ?? string.Empty;
                string credentialResource = GetPasteAICredentialResource(serviceType);
                string credentialUserName = GetPasteAICredentialUserName(serviceType);
                string endpointCredentialUserName = GetPasteAIEndpointCredentialUserName(serviceType);
                PasswordVault vault = new();
                TryRemoveCredential(vault, credentialResource, credentialUserName);
                TryRemoveCredential(vault, credentialResource, endpointCredentialUserName);

                bool storeApiKey = RequiresCredentialStorage(serviceType) && !string.IsNullOrWhiteSpace(apiKey);
                if (storeApiKey)
                {
                    PasswordCredential cred = new(credentialResource, credentialUserName, apiKey);
                    vault.Add(cred);
                }

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    PasswordCredential endpointCred = new(credentialResource, endpointCredentialUserName, endpoint);
                    vault.Add(endpointCred);
                }

                OnPropertyChanged(nameof(IsAIEnabled));
                NotifySettingsChanged();
            }
            catch (Exception)
            {
            }
        }

        internal string GetAdvancedAIApiKey(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return RetrieveCredentialValue(
                GetAdvancedAICredentialResource(serviceType),
                GetAdvancedAICredentialUserName(serviceType));
        }

        internal string GetPasteAIApiKey(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return RetrieveCredentialValue(
                GetPasteAICredentialResource(serviceType),
                GetPasteAICredentialUserName(serviceType));
        }

        internal string GetAdvancedAIEndpoint(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return RetrieveCredentialValue(
                GetAdvancedAICredentialResource(serviceType),
                GetAdvancedAIEndpointCredentialUserName(serviceType));
        }

        internal string GetPasteAIEndpoint(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return RetrieveCredentialValue(
                GetPasteAICredentialResource(serviceType),
                GetPasteAIEndpointCredentialUserName(serviceType));
        }

        private string GetAdvancedAICredentialResource(string serviceType)
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

        private string GetAdvancedAICredentialUserName(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return serviceType.ToLowerInvariant() switch
            {
                "openai" => "PowerToys_AdvancedPaste_AdvancedAI_OpenAI",
                "azureopenai" => "PowerToys_AdvancedPaste_AdvancedAI_AzureOpenAI",
                "azureaiinference" => "PowerToys_AdvancedPaste_AdvancedAI_AzureAIInference",
                "mistral" => "PowerToys_AdvancedPaste_AdvancedAI_Mistral",
                "google" => "PowerToys_AdvancedPaste_AdvancedAI_Google",
                "huggingface" => "PowerToys_AdvancedPaste_AdvancedAI_HuggingFace",
                "anthropic" => "PowerToys_AdvancedPaste_AdvancedAI_Anthropic",
                "amazonbedrock" => "PowerToys_AdvancedPaste_AdvancedAI_AmazonBedrock",
                "ollama" => "PowerToys_AdvancedPaste_AdvancedAI_Ollama",
                _ => "PowerToys_AdvancedPaste_AdvancedAI_OpenAI",
            };
        }

        private string GetAdvancedAIEndpointCredentialUserName(string serviceType)
        {
            return GetAdvancedAICredentialUserName(serviceType) + "_Endpoint";
        }

        private string GetPasteAICredentialResource(string serviceType)
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

        private string GetPasteAICredentialUserName(string serviceType)
        {
            serviceType = string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
            return serviceType.ToLowerInvariant() switch
            {
                "openai" => "PowerToys_AdvancedPaste_PasteAI_OpenAI",
                "azureopenai" => "PowerToys_AdvancedPaste_PasteAI_AzureOpenAI",
                "azureaiinference" => "PowerToys_AdvancedPaste_PasteAI_AzureAIInference",
                "onnx" => "PowerToys_AdvancedPaste_PasteAI_Onnx", // Onnx doesn't need credentials but keeping consistency
                "mistral" => "PowerToys_AdvancedPaste_PasteAI_Mistral",
                "google" => "PowerToys_AdvancedPaste_PasteAI_Google",
                "huggingface" => "PowerToys_AdvancedPaste_PasteAI_HuggingFace",
                "anthropic" => "PowerToys_AdvancedPaste_PasteAI_Anthropic",
                "amazonbedrock" => "PowerToys_AdvancedPaste_PasteAI_AmazonBedrock",
                "ollama" => "PowerToys_AdvancedPaste_PasteAI_Ollama",
                _ => "PowerToys_AdvancedPaste_PasteAI_OpenAI",
            };
        }

        private string GetPasteAIEndpointCredentialUserName(string serviceType)
        {
            return GetPasteAICredentialUserName(serviceType) + "_Endpoint";
        }

        private static bool RequiresCredentialStorage(string serviceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                return true;
            }

            return serviceType.ToLowerInvariant() switch
            {
                "onnx" => false,
                "ollama" => false,
                "foundrylocal" => false,
                "windowsml" => false,
                "anthropic" => false,
                "amazonbedrock" => false,
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
            SubscribeToAdvancedAIConfiguration(_advancedPasteSettings.Properties.AdvancedAIConfiguration);
            SubscribeToPasteAIConfiguration(_advancedPasteSettings.Properties.PasteAIConfiguration);
        }

        private void SubscribeToAdvancedAIConfiguration(AdvancedAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged += OnAdvancedAIConfigurationPropertyChanged;
            }
        }

        private void UnsubscribeFromAdvancedAIConfiguration(AdvancedAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged -= OnAdvancedAIConfigurationPropertyChanged;
            }
        }

        private void SubscribeToPasteAIConfiguration(PasteAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged += OnPasteAIConfigurationPropertyChanged;
            }
        }

        private void UnsubscribeFromPasteAIConfiguration(PasteAIConfiguration configuration)
        {
            if (configuration is not null)
            {
                configuration.PropertyChanged -= OnPasteAIConfigurationPropertyChanged;
            }
        }

        private void OnAdvancedAIConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingAdvancedAIProviderConfiguration)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(AdvancedAIConfiguration.ServiceType), StringComparison.Ordinal))
            {
                LoadAdvancedAIProviderConfiguration();
                SaveAndNotifySettings();
                return;
            }

            if (e.PropertyName is not null && AdvancedAITrackedProperties.Contains(e.PropertyName))
            {
                PersistAdvancedAIProviderConfiguration();
            }

            SaveAndNotifySettings();
        }

        private void OnPasteAIConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingPasteAIProviderConfiguration)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(PasteAIConfiguration.ServiceType), StringComparison.Ordinal))
            {
                LoadPasteAIProviderConfiguration();
                SaveAndNotifySettings();
                return;
            }

            if (e.PropertyName is not null && PasteAITrackedProperties.Contains(e.PropertyName))
            {
                PersistPasteAIProviderConfiguration();
            }

            SaveAndNotifySettings();
        }

        private void SeedProviderConfigurationSnapshots()
        {
            var advancedConfig = _advancedPasteSettings?.Properties?.AdvancedAIConfiguration;
            if (advancedConfig is not null && !advancedConfig.HasProviderConfiguration(advancedConfig.ServiceType))
            {
                advancedConfig.SetProviderConfiguration(
                    advancedConfig.ServiceType,
                    new AIProviderConfigurationSnapshot
                    {
                        ModelName = advancedConfig.ModelName,
                        EndpointUrl = advancedConfig.EndpointUrl,
                        ApiVersion = advancedConfig.ApiVersion,
                        DeploymentName = advancedConfig.DeploymentName,
                        ModelPath = advancedConfig.ModelPath,
                        SystemPrompt = advancedConfig.SystemPrompt,
                        ModerationEnabled = advancedConfig.ModerationEnabled,
                    });
            }

            var pasteConfig = _advancedPasteSettings?.Properties?.PasteAIConfiguration;
            if (pasteConfig is not null && !pasteConfig.HasProviderConfiguration(pasteConfig.ServiceType))
            {
                pasteConfig.SetProviderConfiguration(
                    pasteConfig.ServiceType,
                    new AIProviderConfigurationSnapshot
                    {
                        ModelName = pasteConfig.ModelName,
                        EndpointUrl = pasteConfig.EndpointUrl,
                        ApiVersion = pasteConfig.ApiVersion,
                        DeploymentName = pasteConfig.DeploymentName,
                        ModelPath = pasteConfig.ModelPath,
                        SystemPrompt = pasteConfig.SystemPrompt,
                        ModerationEnabled = pasteConfig.ModerationEnabled,
                    });
            }
        }

        private void LoadAdvancedAIProviderConfiguration()
        {
            var config = _advancedPasteSettings?.Properties?.AdvancedAIConfiguration;
            if (config is null)
            {
                return;
            }

            var snapshot = config.GetOrCreateProviderConfiguration(config.ServiceType);
            _isLoadingAdvancedAIProviderConfiguration = true;
            try
            {
                config.ModelName = snapshot.ModelName ?? string.Empty;
                config.EndpointUrl = snapshot.EndpointUrl ?? string.Empty;
                config.ApiVersion = snapshot.ApiVersion ?? string.Empty;
                config.DeploymentName = snapshot.DeploymentName ?? string.Empty;
                config.ModelPath = snapshot.ModelPath ?? string.Empty;
                config.SystemPrompt = snapshot.SystemPrompt ?? string.Empty;
                config.ModerationEnabled = snapshot.ModerationEnabled;
                string storedEndpoint = GetAdvancedAIEndpoint(config.ServiceType);
                config.EndpointUrl = storedEndpoint;
                snapshot.EndpointUrl = storedEndpoint;
            }
            finally
            {
                _isLoadingAdvancedAIProviderConfiguration = false;
            }
        }

        private void LoadPasteAIProviderConfiguration()
        {
            var config = _advancedPasteSettings?.Properties?.PasteAIConfiguration;
            if (config is null)
            {
                return;
            }

            var snapshot = config.GetOrCreateProviderConfiguration(config.ServiceType);
            _isLoadingPasteAIProviderConfiguration = true;
            try
            {
                config.ModelName = snapshot.ModelName ?? string.Empty;
                config.EndpointUrl = snapshot.EndpointUrl ?? string.Empty;
                config.ApiVersion = snapshot.ApiVersion ?? string.Empty;
                config.DeploymentName = snapshot.DeploymentName ?? string.Empty;
                config.ModelPath = snapshot.ModelPath ?? string.Empty;
                config.SystemPrompt = snapshot.SystemPrompt ?? string.Empty;
                config.ModerationEnabled = snapshot.ModerationEnabled;
                string storedEndpoint = GetPasteAIEndpoint(config.ServiceType);
                config.EndpointUrl = storedEndpoint;
                snapshot.EndpointUrl = storedEndpoint;
            }
            finally
            {
                _isLoadingPasteAIProviderConfiguration = false;
            }
        }

        private void PersistAdvancedAIProviderConfiguration()
        {
            if (_isLoadingAdvancedAIProviderConfiguration)
            {
                return;
            }

            var config = _advancedPasteSettings?.Properties?.AdvancedAIConfiguration;
            if (config is null)
            {
                return;
            }

            var snapshot = config.GetOrCreateProviderConfiguration(config.ServiceType);
            snapshot.ModelName = config.ModelName ?? string.Empty;
            snapshot.EndpointUrl = config.EndpointUrl ?? string.Empty;
            snapshot.ApiVersion = config.ApiVersion ?? string.Empty;
            snapshot.DeploymentName = config.DeploymentName ?? string.Empty;
            snapshot.ModelPath = config.ModelPath ?? string.Empty;
            snapshot.SystemPrompt = config.SystemPrompt ?? string.Empty;
            snapshot.ModerationEnabled = config.ModerationEnabled;
        }

        private void PersistPasteAIProviderConfiguration()
        {
            if (_isLoadingPasteAIProviderConfiguration)
            {
                return;
            }

            var config = _advancedPasteSettings?.Properties?.PasteAIConfiguration;
            if (config is null)
            {
                return;
            }

            var snapshot = config.GetOrCreateProviderConfiguration(config.ServiceType);
            snapshot.ModelName = config.ModelName ?? string.Empty;
            snapshot.EndpointUrl = config.EndpointUrl ?? string.Empty;
            snapshot.ApiVersion = config.ApiVersion ?? string.Empty;
            snapshot.DeploymentName = config.DeploymentName ?? string.Empty;
            snapshot.ModelPath = config.ModelPath ?? string.Empty;
            snapshot.SystemPrompt = config.SystemPrompt ?? string.Empty;
            snapshot.ModerationEnabled = config.ModerationEnabled;
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
