// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Timers;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ScreenTranslatorViewModel : PageViewModelBase
    {
        public const string AzureCredentialResource = "https://api.cognitive.microsofttranslator.com";

        public const string AzureCredentialUsername = "PowerToys_ScreenTranslator_AzureTranslator";

        public const string LibreTranslateCredentialResource = "https://libretranslate.com";

        public const string LibreTranslateCredentialUsername = "PowerToys_ScreenTranslator_LibreTranslate";

        protected override string ModuleName => ScreenTranslatorSettings.ModuleName;

        private bool _disposed;

        private const int SaveSettingsDelayInMs = 500;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly SettingsUtils _settingsUtils;

        private readonly System.Threading.Lock _delayedActionLock = new System.Threading.Lock();

        private readonly ScreenTranslatorSettings _screenTranslatorSettings;

        private Timer _delayedTimer;

        private bool _enabledStateIsGPOConfigured;

        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public static readonly string[] ProviderKeys = ["Passthrough", "AzureTranslator", "LibreTranslate"];

        public static readonly (string Code, string DisplayName)[] SupportedSourceLanguages =
        [
            ("auto", "Auto-detect / System"),
            ("ja-JP", "Japanese (ja-JP)"),
            ("zh-Hans", "Chinese Simplified (zh-Hans)"),
            ("zh-Hant", "Chinese Traditional (zh-Hant)"),
            ("en-US", "English (en-US)"),
            ("ko-KR", "Korean (ko-KR)"),
            ("fr-FR", "French (fr-FR)"),
            ("de-DE", "German (de-DE)"),
            ("es-ES", "Spanish (es-ES)"),
        ];

        public static readonly (string Code, string DisplayName)[] SupportedTargetLanguages =
        [
            ("en-US", "English (en-US)"),
            ("ja-JP", "Japanese (ja-JP)"),
            ("zh-Hans", "Chinese Simplified (zh-Hans)"),
            ("zh-Hant", "Chinese Traditional (zh-Hant)"),
            ("ko-KR", "Korean (ko-KR)"),
            ("fr-FR", "French (fr-FR)"),
            ("de-DE", "German (de-DE)"),
            ("es-ES", "Spanish (es-ES)"),
        ];

        public ObservableCollection<string> AvailableProviders { get; } = new ObservableCollection<string>
        {
            "Passthrough (Offline Prototype)",
            "Azure AI Translator (Cloud v3)",
            "LibreTranslate (Self-Hosted / Cloud)",
        };

        public ObservableCollection<string> AvailableSourceLanguages { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> AvailableTargetLanguages { get; } = new ObservableCollection<string>();

        public ScreenTranslatorViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<ScreenTranslatorSettings> screenTranslatorSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            ArgumentNullException.ThrowIfNull(screenTranslatorSettingsRepository);
            _screenTranslatorSettings = screenTranslatorSettingsRepository.SettingsConfig ?? new ScreenTranslatorSettings();

            InitializeEnabledValue();
            InitializeLanguageLists();

            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;
        }

        private void InitializeLanguageLists()
        {
            AvailableSourceLanguages.Clear();
            foreach (var lang in SupportedSourceLanguages)
            {
                AvailableSourceLanguages.Add(lang.DisplayName);
            }

            AvailableTargetLanguages.Clear();
            foreach (var lang in SupportedTargetLanguages)
            {
                AvailableTargetLanguages.Add(lang.DisplayName);
            }
        }

        private void InitializeEnabledValue()
        {
            _enabledStateIsGPOConfigured = false;
            _isEnabled = GeneralSettingsConfig.Enabled.ScreenTranslator;
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            return new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ActivationShortcut],
            };
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    GeneralSettingsConfig.Enabled.ScreenTranslator = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsWin11OrGreater => OSVersionHelper.IsWindows11();

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

        public HotkeySettings ActivationShortcut
        {
            get => _screenTranslatorSettings.Properties.ActivationShortcut;
            set
            {
                if (_screenTranslatorSettings.Properties.ActivationShortcut != value)
                {
                    _screenTranslatorSettings.Properties.ActivationShortcut = value ?? _screenTranslatorSettings.Properties.DefaultActivationShortcut;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    _settingsUtils.SaveSettings(_screenTranslatorSettings.ToJsonString(), ScreenTranslatorSettings.ModuleName);
                    NotifySettingsChanged();
                }
            }
        }

        public int SelectedProviderIndex
        {
            get
            {
                string provider = _screenTranslatorSettings.Properties.SelectedProvider;
                for (int i = 0; i < ProviderKeys.Length; i++)
                {
                    if (string.Equals(ProviderKeys[i], provider, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return 0;
            }

            set
            {
                if (value >= 0 && value < ProviderKeys.Length)
                {
                    string selected = ProviderKeys[value];
                    if (!string.Equals(_screenTranslatorSettings.Properties.SelectedProvider, selected, StringComparison.Ordinal))
                    {
                        _screenTranslatorSettings.Properties.SelectedProvider = selected;
                        OnPropertyChanged(nameof(SelectedProviderIndex));
                        OnPropertyChanged(nameof(IsAzureProviderSelected));
                        OnPropertyChanged(nameof(IsLibreTranslateProviderSelected));
                        SaveAndNotifySettings();
                    }
                }
            }
        }

        public bool IsAzureProviderSelected => SelectedProviderIndex == 1;

        public bool IsLibreTranslateProviderSelected => SelectedProviderIndex == 2;

        public bool EnableCloudConsent
        {
            get => _screenTranslatorSettings.Properties.EnableCloudConsent;
            set
            {
                if (_screenTranslatorSettings.Properties.EnableCloudConsent != value)
                {
                    _screenTranslatorSettings.Properties.EnableCloudConsent = value;
                    OnPropertyChanged(nameof(EnableCloudConsent));
                    SaveAndNotifySettings();
                }
            }
        }

        public int SourceLanguageIndex
        {
            get
            {
                string code = _screenTranslatorSettings.Properties.SourceLanguage;
                for (int i = 0; i < SupportedSourceLanguages.Length; i++)
                {
                    if (string.Equals(SupportedSourceLanguages[i].Code, code, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return 0;
            }

            set
            {
                if (value >= 0 && value < SupportedSourceLanguages.Length)
                {
                    string code = SupportedSourceLanguages[value].Code;
                    if (!string.Equals(_screenTranslatorSettings.Properties.SourceLanguage, code, StringComparison.Ordinal))
                    {
                        _screenTranslatorSettings.Properties.SourceLanguage = code;
                        OnPropertyChanged(nameof(SourceLanguageIndex));
                        SaveAndNotifySettings();
                    }
                }
            }
        }

        public int TargetLanguageIndex
        {
            get
            {
                string code = _screenTranslatorSettings.Properties.TargetLanguage;
                for (int i = 0; i < SupportedTargetLanguages.Length; i++)
                {
                    if (string.Equals(SupportedTargetLanguages[i].Code, code, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return 0;
            }

            set
            {
                if (value >= 0 && value < SupportedTargetLanguages.Length)
                {
                    string code = SupportedTargetLanguages[value].Code;
                    if (!string.Equals(_screenTranslatorSettings.Properties.TargetLanguage, code, StringComparison.Ordinal))
                    {
                        _screenTranslatorSettings.Properties.TargetLanguage = code;
                        OnPropertyChanged(nameof(TargetLanguageIndex));
                        SaveAndNotifySettings();
                    }
                }
            }
        }

        public string AzureEndpoint
        {
            get => _screenTranslatorSettings.Properties.AzureEndpoint;
            set
            {
                if (_screenTranslatorSettings.Properties.AzureEndpoint != value)
                {
                    _screenTranslatorSettings.Properties.AzureEndpoint = value;
                    OnPropertyChanged(nameof(AzureEndpoint));
                    SaveAndNotifySettings();
                }
            }
        }

        public string AzureRegion
        {
            get => _screenTranslatorSettings.Properties.AzureRegion;
            set
            {
                if (_screenTranslatorSettings.Properties.AzureRegion != value)
                {
                    _screenTranslatorSettings.Properties.AzureRegion = value;
                    OnPropertyChanged(nameof(AzureRegion));
                    SaveAndNotifySettings();
                }
            }
        }

        public string LibreTranslateEndpoint
        {
            get => _screenTranslatorSettings.Properties.LibreTranslateEndpoint;
            set
            {
                if (_screenTranslatorSettings.Properties.LibreTranslateEndpoint != value)
                {
                    _screenTranslatorSettings.Properties.LibreTranslateEndpoint = value;
                    OnPropertyChanged(nameof(LibreTranslateEndpoint));
                    SaveAndNotifySettings();
                }
            }
        }

        public bool HasAzureApiKey => !string.IsNullOrWhiteSpace(RetrieveCredential(AzureCredentialResource, AzureCredentialUsername));

        public bool HasLibreTranslateApiKey => !string.IsNullOrWhiteSpace(RetrieveCredential(LibreTranslateCredentialResource, LibreTranslateCredentialUsername));

        public void SaveAzureApiKey(string key)
        {
            SaveCredential(AzureCredentialResource, AzureCredentialUsername, key);
            OnPropertyChanged(nameof(HasAzureApiKey));
        }

        public void RemoveAzureApiKey()
        {
            RemoveCredential(AzureCredentialResource, AzureCredentialUsername);
            OnPropertyChanged(nameof(HasAzureApiKey));
        }

        public void SaveLibreTranslateApiKey(string key)
        {
            SaveCredential(LibreTranslateCredentialResource, LibreTranslateCredentialUsername, key);
            OnPropertyChanged(nameof(HasLibreTranslateApiKey));
        }

        public void RemoveLibreTranslateApiKey()
        {
            RemoveCredential(LibreTranslateCredentialResource, LibreTranslateCredentialUsername);
            OnPropertyChanged(nameof(HasLibreTranslateApiKey));
        }

        private static string RetrieveCredential(string resource, string username)
        {
            try
            {
                var vault = new PasswordVault();
                var cred = vault.Retrieve(resource, username);
                cred?.RetrievePassword();
                return cred?.Password?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void SaveCredential(string resource, string username, string secret)
        {
            try
            {
                var vault = new PasswordVault();
                RemoveCredential(resource, username);
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    var cred = new PasswordCredential(resource, username, secret.Trim());
                    vault.Add(cred);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save credential in PasswordVault: {ex.Message}");
            }
        }

        private static void RemoveCredential(string resource, string username)
        {
            try
            {
                var vault = new PasswordVault();
                var cred = vault.Retrieve(resource, username);
                if (cred != null)
                {
                    vault.Remove(cred);
                }
            }
            catch
            {
                // Credential doesn't exist, which is fine
            }
        }

        private void SaveAndNotifySettings()
        {
            _settingsUtils.SaveSettings(_screenTranslatorSettings.ToJsonString(), ScreenTranslatorSettings.ModuleName);
            ScheduleSavingOfSettings();
        }

        private void ScheduleSavingOfSettings()
        {
            lock (_delayedActionLock)
            {
                if (_delayedTimer.Enabled)
                {
                    _delayedTimer.Stop();
                }

                _delayedTimer.Start();
            }
        }

        private void DelayedTimer_Tick(object sender, ElapsedEventArgs e)
        {
            lock (_delayedActionLock)
            {
                _delayedTimer.Stop();
                NotifySettingsChanged();
            }
        }

        private void NotifySettingsChanged()
        {
            try
            {
                // Using InvariantCulture as this is an IPC message
                SendConfigMSG(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                        ScreenTranslatorSettings.ModuleName,
                        JsonSerializer.Serialize(_screenTranslatorSettings, SourceGenerationContextContext.Default.ScreenTranslatorSettings)));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to notify settings changed: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _delayedTimer?.Dispose();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
