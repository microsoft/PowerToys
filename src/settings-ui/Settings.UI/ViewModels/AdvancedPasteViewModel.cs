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
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.AI.Credentials;
using Microsoft.PowerToys.AI.Telemetry;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AdvancedPasteViewModel : PageViewModelBase
    {
        private static readonly HashSet<string> WarnHotkeys = ["Ctrl + V", "Ctrl + Shift + V"];

        private bool _disposed;
        private AIConfiguration _aiConfiguration;

        protected override string ModuleName => AdvancedPasteSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly SettingsUtils _settingsUtils;

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

        private static readonly HashSet<string> CustomActionNonPersistedProperties = new(StringComparer.Ordinal)
        {
            nameof(AdvancedPasteCustomAction.CanMoveUp),
            nameof(AdvancedPasteCustomAction.CanMoveDown),
            nameof(AdvancedPasteCustomAction.IsValid),
            nameof(AdvancedPasteCustomAction.HasConflict),
            nameof(AdvancedPasteCustomAction.Tooltip),
            nameof(AdvancedPasteCustomAction.SubActions),
        };

        public AdvancedPasteViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<AdvancedPasteSettings> advancedPasteSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            ArgumentNullException.ThrowIfNull(advancedPasteSettingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _advancedPasteSettings = advancedPasteSettingsRepository.SettingsConfig
                ?? throw new ArgumentException("SettingsConfig cannot be null", nameof(advancedPasteSettingsRepository));

            if (_advancedPasteSettings.Properties is null)
            {
                throw new ArgumentException("AdvancedPasteSettings.Properties cannot be null", nameof(advancedPasteSettingsRepository));
            }

            _advancedPasteSettings.Properties.AdditionalActions ??= new AdvancedPasteAdditionalActions();
            _advancedPasteSettings.Properties.CustomActions ??= new AdvancedPasteCustomActions();

            SendConfigMSG = ipcMSGCallBackFunc;

            _additionalActions = _advancedPasteSettings.Properties.AdditionalActions;
            _customActions = _advancedPasteSettings.Properties.CustomActions.Value ?? new ObservableCollection<AdvancedPasteCustomAction>();

            SetupSettingsFileWatcher();
            LoadGlobalAIConfiguration();
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

                    if (ReferenceEquals(additionalAction, _additionalActions.FixSpellingAndGrammar)
                        && additionalAction.CoachingEnabled
                        && additionalAction.CoachingShortcut is { Code: not 0 })
                    {
                        hotkeySettings.Add(additionalAction.CoachingShortcut);
                    }
                }
            }

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
                DisableAI();
            }
        }

        private void LoadGlobalAIConfiguration()
        {
            bool migrated = AIProvidersMigrationHelper.EnsureMigrated(_settingsUtils);
            if (migrated)
            {
                PowerToysTelemetry.Log.WriteEvent(new AISettingsMigrationEvent(
                    AIProvidersSettings.ModuleName,
                    "AdvancedPasteToGlobal",
                    succeeded: true));
            }

            AIProvidersSettings globalSettings;
            try
            {
                globalSettings = _settingsUtils.GetSettingsOrDefault<AIProvidersSettings>(AIProvidersSettings.ModuleName);
            }
            catch
            {
                globalSettings = new AIProvidersSettings();
            }

            globalSettings.Properties ??= new AIProvidersProperties();
            globalSettings.Properties.AIConfiguration ??= new AIConfiguration();
            globalSettings.Properties.AIConfiguration.Providers ??= new ObservableCollection<AIProviderDefinition>();

            _aiConfiguration = globalSettings.Properties.AIConfiguration;
            SyncProviderActiveFlags(_aiConfiguration);

            // Keep legacy Advanced Paste paste-ai-configuration in sync for module runtime until it reads global settings.
            _advancedPasteSettings.Properties.PasteAIConfiguration = _aiConfiguration;
        }

        public void ReloadGlobalAIConfiguration()
        {
            LoadGlobalAIConfiguration();
            OnPropertyChanged(nameof(AIConfiguration));
            OnPropertyChanged(nameof(ActiveProviderDisplayName));
        }

        private void MigrateLegacyAIEnablement()
        {
            var properties = _advancedPasteSettings?.Properties;
            if (properties is null)
            {
                return;
            }

            bool legacyAdvancedAIConsumed = properties.TryConsumeLegacyAdvancedAIEnabled(out var advancedFlag);
            bool legacyAdvancedAIEnabled = legacyAdvancedAIConsumed && advancedFlag;

            if (IsOnlineAIModelsDisallowedByGPO)
            {
                if (legacyAdvancedAIConsumed)
                {
                    SaveAndNotifySettings();
                }

                return;
            }

            PasswordCredential legacyCredential = TryGetLegacyOpenAICredential();

            if (legacyCredential is null)
            {
                if (legacyAdvancedAIConsumed)
                {
                    SaveAndNotifySettings();
                }

                return;
            }

            var configuration = AIConfiguration;
            bool configurationUpdated = false;

            var ensureResult = AdvancedPasteMigrationHelper.EnsureOpenAIProvider(configuration);
            AIProviderDefinition openAIProvider = ensureResult.Provider;
            configurationUpdated |= ensureResult.Updated;

            if (legacyAdvancedAIConsumed && openAIProvider is not null && openAIProvider.EnableAdvancedAI != legacyAdvancedAIEnabled)
            {
                openAIProvider.EnableAdvancedAI = legacyAdvancedAIEnabled;
                configurationUpdated = true;
            }

            if (legacyCredential is not null && openAIProvider is not null)
            {
                AICredentialVault.SaveKey(AIServiceType.OpenAI, openAIProvider.Id, legacyCredential.Password);
                RemoveLegacyOpenAICredential();
                configurationUpdated = true;
            }

            const bool shouldEnableAI = true;
            bool enabledChanged = false;
            if (properties.IsAIEnabled != shouldEnableAI)
            {
                properties.IsAIEnabled = shouldEnableAI;
                enabledChanged = true;
            }

            bool shouldPersist = configurationUpdated || enabledChanged || legacyAdvancedAIConsumed;

            if (shouldPersist)
            {
                if (configurationUpdated)
                {
                    PersistGlobalAIConfiguration();
                }

                SaveAndNotifySettings();

                if (configurationUpdated)
                {
                    OnPropertyChanged(nameof(AIConfiguration));
                    OnPropertyChanged(nameof(ActiveProviderDisplayName));
                }

                if (enabledChanged)
                {
                    OnPropertyChanged(nameof(IsAIEnabled));
                }
            }
        }

        private void PersistGlobalAIConfiguration()
        {
            var globalSettings = new AIProvidersSettings
            {
                Properties = new AIProvidersProperties
                {
                    AIConfiguration = AIConfiguration,
                },
            };
            globalSettings.Save(_settingsUtils);
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
                    OnPropertyChanged(nameof(ShowOnlineAIModelsGpoConfiguredInfoBar));
                    OnPropertyChanged(nameof(ShowClipboardHistoryIsGpoConfiguredInfoBar));

                    GeneralSettingsConfig.Enabled.AdvancedPaste = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public ObservableCollection<AdvancedPasteCustomAction> CustomActions => _customActions;

        public AdvancedPasteAdditionalActions AdditionalActions => _additionalActions;

        public bool IsAIEnabled => _advancedPasteSettings.Properties.IsAIEnabled && !IsOnlineAIModelsDisallowedByGPO;

        public AIConfiguration AIConfiguration => _aiConfiguration ??= new AIConfiguration();

        public string ActiveProviderDisplayName
        {
            get
            {
                var active = AIConfiguration?.ActiveProvider;
                if (active is null)
                {
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(active.DisplayName) ? active.ServiceType : active.DisplayName;
            }
        }

        private PasswordCredential TryGetLegacyOpenAICredential()
        {
            try
            {
                PasswordVault vault = new();
                var credential = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                credential?.RetrievePassword();
                return credential;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void RemoveLegacyOpenAICredential()
        {
            try
            {
                PasswordVault vault = new();
                PasswordCredential existingCred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                vault.Remove(existingCred);
            }
            catch (Exception)
            {
            }
        }

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

        public bool IsOnlineAIModelsDisallowedByGPO =>
            _onlineAIModelsDisallowedByGPO || _enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;

        public bool ShowOnlineAIModelsGpoConfiguredInfoBar => _onlineAIModelsDisallowedByGPO && _isEnabled;

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

                return false;
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

        public bool ClipboardHistoryDisabledByGPO => IsClipboardHistoryDisabledByGPO();

        public bool ShowClipboardHistoryIsGpoConfiguredInfoBar => IsClipboardHistoryDisabledByGPO() && _isEnabled;

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

        public bool ShowAIPaste
        {
            get => _advancedPasteSettings.Properties.ShowAIPaste;
            set
            {
                if (value != _advancedPasteSettings.Properties.ShowAIPaste)
                {
                    _advancedPasteSettings.Properties.ShowAIPaste = value;
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

        public bool EnableClipboardPreview
        {
            get => _advancedPasteSettings.Properties.EnableClipboardPreview;
            set
            {
                if (value != _advancedPasteSettings.Properties.EnableClipboardPreview)
                {
                    _advancedPasteSettings.Properties.EnableClipboardPreview = value;
                    NotifySettingsChanged();
                }
            }
        }

        public bool AutoCopySelectionForCustomActionHotkey
        {
            get => _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey;
            set
            {
                if (value != _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey)
                {
                    _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey = value;
                    NotifySettingsChanged();
                }
            }
        }

        public bool IsConflictingCopyShortcut =>
            _customActions.Select(customAction => customAction.Shortcut)
                          .Concat([PasteAsPlainTextShortcut, AdvancedPasteUIShortcut, PasteAsMarkdownShortcut, PasteAsJsonShortcut])
                          .Any(hotkey => WarnHotkeys.Contains(hotkey.ToString()));

        public bool IsAdditionalActionConflictingCopyShortcut
        {
            get
            {
                var shortcuts = _additionalActions.GetAllActions()
                                                  .OfType<AdvancedPasteAdditionalAction>()
                                                  .Select(additionalAction => additionalAction.Shortcut)
                                                  .ToList();

                var fixSpelling = _additionalActions.FixSpellingAndGrammar;
                if (fixSpelling.CoachingEnabled && fixSpelling.CoachingShortcut is { Code: not 0 })
                {
                    shortcuts.Add(fixSpelling.CoachingShortcut);
                }

                return shortcuts.Any(hotkey => WarnHotkeys.Contains(hotkey.ToString()));
            }
        }

        private void NotifySettingsChanged()
        {
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

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
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

            // Persist global AI configuration mirror for Advanced Paste consumers.
            _advancedPasteSettings.Properties.PasteAIConfiguration = AIConfiguration;
            _settingsUtils.SaveSettings(_advancedPasteSettings.ToJsonString(), AdvancedPasteSettings.ModuleName);
            NotifySettingsChanged();
        }

        private void OnAdditionalActionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SaveAndNotifySettings();

            if (e.PropertyName is nameof(AdvancedPasteAdditionalAction.Shortcut)
                or nameof(AdvancedPasteAdditionalAction.CoachingShortcut)
                or nameof(AdvancedPasteAdditionalAction.CoachingEnabled))
            {
                OnPropertyChanged(nameof(IsAdditionalActionConflictingCopyShortcut));
            }
        }

        private void OnCustomActionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && !CustomActionNonPersistedProperties.Contains(e.PropertyName))
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

            if (target.ShowAIPaste != source.ShowAIPaste)
            {
                target.ShowAIPaste = source.ShowAIPaste;
                OnPropertyChanged(nameof(ShowAIPaste));
            }

            if (target.CloseAfterLosingFocus != source.CloseAfterLosingFocus)
            {
                target.CloseAfterLosingFocus = source.CloseAfterLosingFocus;
                OnPropertyChanged(nameof(CloseAfterLosingFocus));
            }

            if (target.EnableClipboardPreview != source.EnableClipboardPreview)
            {
                target.EnableClipboardPreview = source.EnableClipboardPreview;
                OnPropertyChanged(nameof(EnableClipboardPreview));
            }

            if (target.AutoCopySelectionForCustomActionHotkey != source.AutoCopySelectionForCustomActionHotkey)
            {
                target.AutoCopySelectionForCustomActionHotkey = source.AutoCopySelectionForCustomActionHotkey;
                OnPropertyChanged(nameof(AutoCopySelectionForCustomActionHotkey));
            }

            ReloadGlobalAIConfiguration();
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
