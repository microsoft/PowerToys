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
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AdvancedPasteViewModel : Observable, IDisposable
    {
        private static readonly HashSet<string> WarnHotkeys = ["Ctrl + V", "Ctrl + Shift + V"];

        private bool disposedValue;

        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it, otherwise we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

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

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;

            foreach (var action in _additionalActions.AllActions)
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
                    OnPropertyChanged(nameof(AdvancedPasteUIShortcut));
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));

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
                    OnPropertyChanged(nameof(PasteAsPlainTextShortcut));
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));

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
                    OnPropertyChanged(nameof(PasteAsMarkdownShortcut));
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));

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
                    OnPropertyChanged(nameof(PasteAsJsonShortcut));
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));

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
            _additionalActions.AllActions
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _delayedTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
