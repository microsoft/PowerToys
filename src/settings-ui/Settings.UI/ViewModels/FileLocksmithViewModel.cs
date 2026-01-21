// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the File Locksmith settings page.
    /// Uses CommunityToolkit.Mvvm for MVVM pattern implementation.
    /// </summary>
    public partial class FileLocksmithViewModel : ObservableObject
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private FileLocksmithSettings Settings { get; set; }

        private const string ModuleNameConst = FileLocksmithSettings.ModuleName;

        private string _settingsConfigFileFolder = string.Empty;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabledGpoConfigured))]
        private bool _enabledStateIsGPOConfigured;

        [ObservableProperty]
        private bool _isFileLocksmithEnabled;

        [ObservableProperty]
        private bool _enabledOnContextExtendedMenu;

        private Func<string, int> SendConfigMSG { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLocksmithViewModel"/> class
        /// using dependency injection (for new code).
        /// </summary>
        /// <param name="settingsUtils">The settings utilities.</param>
        /// <param name="generalSettingsRepository">The general settings repository.</param>
        /// <param name="sendConfigMSG">The IPC message callback.</param>
        public FileLocksmithViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> generalSettingsRepository,
            Func<string, int> sendConfigMSG)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            GeneralSettingsConfig = generalSettingsRepository.SettingsConfig;
            SendConfigMSG = sendConfigMSG ?? throw new ArgumentNullException(nameof(sendConfigMSG));

            LoadSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLocksmithViewModel"/> class
        /// (backward compatible constructor for existing code).
        /// </summary>
        public FileLocksmithViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _settingsConfigFileFolder = configFileSubfolder;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                FileLocksmithLocalProperties localSettings = ((SettingsUtils)_settingsUtils).GetSettingsOrDefault<FileLocksmithLocalProperties>(GetSettingsSubPath(), "file-locksmith-settings.json");
                Settings = new FileLocksmithSettings(localSettings);
            }
            catch (Exception)
            {
                FileLocksmithLocalProperties localSettings = new FileLocksmithLocalProperties();
                Settings = new FileLocksmithSettings(localSettings);
                _settingsUtils.SaveSettings(localSettings.ToJsonString(), GetSettingsSubPath(), "file-locksmith-settings.json");
            }

            InitializeEnabledValue();
            EnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleNameConst;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                EnabledStateIsGPOConfigured = true;
                IsFileLocksmithEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                IsFileLocksmithEnabled = GeneralSettingsConfig.Enabled.FileLocksmith;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the enabled state is configured by GPO.
        /// </summary>
        public bool IsEnabledGpoConfigured => EnabledStateIsGPOConfigured;

        partial void OnIsFileLocksmithEnabledChanged(bool value)
        {
            if (EnabledStateIsGPOConfigured)
            {
                // If it's GPO configured, shouldn't be able to change this state.
                return;
            }

            GeneralSettingsConfig.Enabled.FileLocksmith = value;

            OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
            SendConfigMSG(outgoing.ToString());

            NotifySettingsChanged();
        }

        partial void OnEnabledOnContextExtendedMenuChanged(bool value)
        {
            Settings.Properties.ExtendedContextMenuOnly.Value = value;
            NotifySettingsChanged();
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       FileLocksmithSettings.ModuleName,
                       JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.FileLocksmithSettings)));
        }

        /// <summary>
        /// Refreshes the enabled state by re-reading GPO configuration.
        /// </summary>
        [RelayCommand]
        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsFileLocksmithEnabled));
        }
    }
}
