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
using System.Timers;
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
        private bool _disposed;

        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it; otherwise, we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

        protected override string ModuleName => AdvancedPasteSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly System.Threading.Lock _delayedActionLock = new System.Threading.Lock();

        private readonly AdvancedPasteSettings _advancedPasteSettings;
        private readonly AdvancedPasteAdditionalActions _additionalActions;
        private readonly ObservableCollection<AdvancedPasteCustomAction> _customActions;
        private Timer _delayedTimer;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private GpoRuleConfigured _onlineAIModelsGpoRuleConfiguration;
        private bool _onlineAIModelsDisallowedByGPO;
        private bool _isEnabled;
        private ObservableCollection<AdvancedPasteAIServiceOption> _aiServiceOptions;
        private AdvancedPasteAIServiceOption _selectedAIService;
        private ObservableCollection<AdvancedPasteAIServiceParameter> _currentAIServiceParameters;

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

            _additionalActions = _advancedPasteSettings.Properties.AdditionalActions;
            _customActions = _advancedPasteSettings.Properties.CustomActions.Value;

            InitializeEnabledValue();

            LoadAIServiceOptionsAndDefaults();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;

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
            if (_onlineAIModelsGpoRuleConfiguration == GpoRuleConfigured.Disabled)
            {
                _onlineAIModelsDisallowedByGPO = true;

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

        public bool IsAIServiceEnabled => SelectedAIService?.Id != "Disabled";

        public ObservableCollection<AdvancedPasteAIServiceOption> AIServiceOptions
        {
            get => _aiServiceOptions;
            set
            {
                if (_aiServiceOptions != value)
                {
                    _aiServiceOptions = value;
                    OnPropertyChanged(nameof(AIServiceOptions));
                }
            }
        }

        public AdvancedPasteAIServiceOption SelectedAIService
        {
            get => _selectedAIService;
            set
            {
                if (_selectedAIService != value)
                {
                    _selectedAIService = value;
                    OnPropertyChanged(nameof(SelectedAIService));
                    OnPropertyChanged(nameof(IsAIServiceEnabled));
                    UpdateCurrentAIServiceParameters();
                }
            }
        }

        public ObservableCollection<AdvancedPasteAIServiceParameter> CurrentAIServiceParameters
        {
            get => _currentAIServiceParameters;
            set
            {
                if (_currentAIServiceParameters != value)
                {
                    _currentAIServiceParameters = value;
                    OnPropertyChanged(nameof(CurrentAIServiceParameters));
                }
            }
        }

        private void LoadAIServiceOptionsAndDefaults()
        {
            AIServiceOptions = new ObservableCollection<AdvancedPasteAIServiceOption>
            {
                new AdvancedPasteAIServiceOption { Id = "AzureOpenAI", DisplayName = "Azure OpenAI" },
                new AdvancedPasteAIServiceOption { Id = "OpenAI", DisplayName = "OpenAI" },
                new AdvancedPasteAIServiceOption { Id = "Mistral", DisplayName = "Mistral" },
                new AdvancedPasteAIServiceOption { Id = "GoogleGemini", DisplayName = "Google Gemini" },
                new AdvancedPasteAIServiceOption { Id = "HuggingFace", DisplayName = "Hugging Face" },
                new AdvancedPasteAIServiceOption { Id = "AzureAIInference", DisplayName = "Azure AI Inference" },
                new AdvancedPasteAIServiceOption { Id = "Ollama", DisplayName = "Ollama" },
                new AdvancedPasteAIServiceOption { Id = "Anthropic", DisplayName = "Anthropic Claude" },
                new AdvancedPasteAIServiceOption { Id = "AmazonBedrock", DisplayName = "Amazon Bedrock" },
                new AdvancedPasteAIServiceOption { Id = "ONNX", DisplayName = "Hugging Face" },
                new AdvancedPasteAIServiceOption { Id = "Other", DisplayName = "Other" },
            };
            SelectedAIService = AIServiceOptions.First();
            UpdateCurrentAIServiceParameters();
        }

        private void UpdateCurrentAIServiceParameters()
        {
            var parameters = new ObservableCollection<AdvancedPasteAIServiceParameter>();
            switch (SelectedAIService?.Id)
            {
                case "Disabled":
                    // No parameters needed for disabled state
                    break;

                case "OpenAI":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Your OpenAI API key from https://platform.openai.com/api-keys",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model",
                        Type = "text",
                        Description = "OpenAI model (e.g., gpt-4o, gpt-4-turbo, gpt-3.5-turbo)",
                    });
                    break;

                case "AzureOpenAI":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "endpoint",
                        DisplayName = "Endpoint",
                        Type = "text",
                        Description = "Azure OpenAI endpoint URL (e.g., https://your-resource.openai.azure.com/)",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Azure OpenAI API key",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "deploymentName",
                        DisplayName = "Deployment Name",
                        Type = "text",
                        Description = "Azure OpenAI deployment name",
                    });
                    break;

                case "AzureAIInference":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Azure AI Inference API key",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model ID",
                        Type = "text",
                        Description = "Model identifier",
                    });
                    break;

                case "Anthropic":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model",
                        Type = "text",
                        Description = "Claude model (e.g., claude-3-5-sonnet-20241022, claude-3-haiku-20240307)",
                    });
                    break;

                case "GoogleGemini":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Google AI Studio API key",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model",
                        Type = "text",
                        Description = "Gemini model (e.g., gemini-1.5-pro, gemini-1.5-flash)",
                    });
                    break;

                case "Mistral":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Mistral AI API key from https://console.mistral.ai/",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model",
                        Type = "text",
                        Description = "Mistral model (e.g., mistral-large-latest, mistral-small-latest)",
                    });
                    break;

                case "HuggingFace":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "Hugging Face API token",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model ID",
                        Type = "text",
                        Description = "Hugging Face model identifier",
                    });
                    break;

                case "Ollama":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "endpoint",
                        DisplayName = "Endpoint",
                        Type = "text",
                        Description = "Ollama server endpoint",
                        Value = "http://localhost:11434",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model",
                        Type = "text",
                        Description = "Ollama model name (e.g., llama3.2, mistral, codellama)",
                    });
                    break;

                case "AmazonBedrock":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model ID",
                        Type = "text",
                        Description = "Bedrock model identifier",
                    });
                    break;

                case "ONNX":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelPath",
                        DisplayName = "Model Path",
                        Type = "text",
                        Description = "Path to ONNX model file",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model ID",
                        Type = "text",
                        Description = "Bedrock model identifier",
                    });
                    break;

                case "Other":
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "endpoint",
                        DisplayName = "Endpoint",
                        Type = "text",
                        Description = "Custom API endpoint URL",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "apiKey",
                        DisplayName = "API Key",
                        Type = "password",
                        Description = "API key for authentication",
                    });
                    parameters.Add(new AdvancedPasteAIServiceParameter
                    {
                        Name = "modelId",
                        DisplayName = "Model ID",
                        Type = "text",
                        Description = "Model identifier",
                    });
                    break;

                default:
                    break;
            }

            CurrentAIServiceParameters = parameters;
        }

        public void SaveAIConfiguration()
        {
            if (SelectedAIService == null || CurrentAIServiceParameters == null)
            {
                return;
            }

            var sensitiveParameters = new HashSet<string> { "endpoint", "apiKey" };
            var serviceId = SelectedAIService.Id;

            var credentialParameters = new Dictionary<string, object>();
            var settingsParameters = new Dictionary<string, object>();

            foreach (var param in CurrentAIServiceParameters)
            {
                var value = param.Value ?? string.Empty;

                if (sensitiveParameters.Contains(param.Name))
                {
                    credentialParameters[param.Name] = value;
                }
                else
                {
                    settingsParameters[param.Name] = value;
                }
            }

            SaveCredentialParameters(serviceId, credentialParameters);

            var aiConfiguration = new Dictionary<string, object>
            {
                ["ServiceId"] = serviceId,
                ["ServiceDisplayName"] = SelectedAIService.DisplayName,
                ["Parameters"] = settingsParameters,
            };

            _advancedPasteSettings.Properties.AIServiceConfiguration = aiConfiguration;

            SaveAndNotifySettings();
        }

        private void SaveCredentialParameters(string serviceId, Dictionary<string, object> credentialParameters)
        {
            try
            {
                PasswordVault vault = new PasswordVault();

                foreach (var parameter in credentialParameters)
                {
                    if (string.IsNullOrEmpty(parameter.Value?.ToString()))
                    {
                        RemoveCredential(vault, serviceId, parameter.Key);
                        continue;
                    }

                    var resourceName = $"PowerToys.AdvancedPaste.{serviceId}";
                    var userName = $"{serviceId}_{parameter.Key}";
                    var password = parameter.Value.ToString();

                    RemoveCredential(vault, resourceName, userName);

                    var credential = new PasswordCredential(resourceName, userName, password);
                    vault.Add(credential);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save credentials: {ex.Message}");
            }
        }

        private void RemoveCredential(PasswordVault vault, string resourceName, string userName)
        {
            try
            {
                var existingCredential = vault.Retrieve(resourceName, userName);
                if (existingCredential != null)
                {
                    vault.Remove(existingCredential);
                }
            }
            catch (Exception)
            {
            }
        }

        private Dictionary<string, string> LoadCredentialParameters(string serviceId)
        {
            var credentials = new Dictionary<string, string>();
            var sensitiveParameters = new HashSet<string> { "endpoint", "apiKey" };

            try
            {
                PasswordVault vault = new PasswordVault();
                var resourceName = $"PowerToys.AdvancedPaste.{serviceId}";

                foreach (var parameterName in sensitiveParameters)
                {
                    try
                    {
                        var userName = $"{serviceId}_{parameterName}";
                        var credential = vault.Retrieve(resourceName, userName);
                        if (credential != null)
                        {
                            credential.RetrievePassword();
                            credentials[parameterName] = credential.Password;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load credentials: {ex.Message}");
            }

            return credentials;
        }

        private void LoadAIConfigurationFromSettings()
        {
            if (_advancedPasteSettings.Properties.AIServiceConfiguration != null)
            {
                var config = _advancedPasteSettings.Properties.AIServiceConfiguration;

                if (config.TryGetValue("ServiceId", out var serviceId))
                {
                    var savedService = AIServiceOptions.FirstOrDefault(s => s.Id == serviceId?.ToString());
                    if (savedService != null)
                    {
                        SelectedAIService = savedService;

                        LoadParameterValues(config, serviceId.ToString());
                    }
                }
            }
        }

        private void LoadParameterValues(Dictionary<string, object> config, string serviceId)
        {
            var settingsParameters = new Dictionary<string, object>();
            if (config.TryGetValue("Parameters", out var parameters) &&
                parameters is Dictionary<string, object> paramDict)
            {
                settingsParameters = paramDict;
            }

            var credentialParameters = LoadCredentialParameters(serviceId);

            foreach (var parameter in CurrentAIServiceParameters)
            {
                if (credentialParameters.TryGetValue(parameter.Name, out var credentialValue))
                {
                    parameter.Value = credentialValue;
                }
                else if (settingsParameters.TryGetValue(parameter.Name, out var settingsValue))
                {
                    parameter.Value = settingsValue;
                }
            }
        }

        private void ClearServiceCredentials(string serviceId)
        {
            try
            {
                PasswordVault vault = new PasswordVault();
                var resourceName = $"PowerToys.AdvancedPaste.{serviceId}";
                var sensitiveParameters = new HashSet<string> { "endpoint", "apiKey" };

                foreach (var parameterName in sensitiveParameters)
                {
                    var userName = $"{serviceId}_{parameterName}";
                    RemoveCredential(vault, resourceName, userName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear service credentials: {ex.Message}");
            }
        }

        private bool OpenAIKeyExists()
        {
            PasswordVault vault = new PasswordVault();
            PasswordCredential cred = null;

            try
            {
                cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
            }
            catch (Exception)
            {
                return false;
            }

            return cred is not null;
        }

        public bool IsOpenAIEnabled => OpenAIKeyExists() && !IsOnlineAIModelsDisallowedByGPO;

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

        private void DelayedTimer_Tick(object sender, EventArgs e)
        {
            lock (_delayedActionLock)
            {
                _delayedTimer.Stop();
                NotifySettingsChanged();
            }
        }

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
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _delayedTimer?.Dispose();

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
                PasswordVault vault = new PasswordVault();
                PasswordCredential cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                vault.Remove(cred);
                OnPropertyChanged(nameof(IsOpenAIEnabled));
                NotifySettingsChanged();
            }
            catch (Exception)
            {
            }
        }

        internal void EnableAI(string password)
        {
            try
            {
                PasswordVault vault = new();
                PasswordCredential cred = new("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey", password);
                vault.Add(cred);
                OnPropertyChanged(nameof(IsOpenAIEnabled));
                IsAdvancedAIEnabled = true; // new users should get Semantic Kernel benefits immediately
                NotifySettingsChanged();
            }
            catch (Exception)
            {
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
