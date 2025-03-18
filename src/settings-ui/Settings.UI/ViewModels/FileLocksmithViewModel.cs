// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class FileLocksmithViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private FileLocksmithSettings Settings { get; set; }

        private const string ModuleName = FileLocksmithSettings.ModuleName;

        private string _settingsConfigFileFolder = string.Empty;

        public FileLocksmithViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            try
            {
                FileLocksmithLocalProperties localSettings = _settingsUtils.GetSettingsOrDefault<FileLocksmithLocalProperties>(GetSettingsSubPath(), "file-locksmith-settings.json");
                Settings = new FileLocksmithSettings(localSettings);
            }
            catch (Exception)
            {
                FileLocksmithLocalProperties localSettings = new FileLocksmithLocalProperties();
                Settings = new FileLocksmithSettings(localSettings);
                _settingsUtils.SaveSettings(localSettings.ToJsonString(), GetSettingsSubPath(), "file-locksmith-settings.json");
            }

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _fileLocksmithEnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isFileLocksmithEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isFileLocksmithEnabled = GeneralSettingsConfig.Enabled.FileLocksmith;
            }
        }

        public bool IsFileLocksmithEnabled
        {
            get => _isFileLocksmithEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isFileLocksmithEnabled != value)
                {
                    _isFileLocksmithEnabled = value;

                    GeneralSettingsConfig.Enabled.FileLocksmith = value;
                    OnPropertyChanged(nameof(IsFileLocksmithEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    // TODO: Implement when this module has properties.
                    NotifySettingsChanged();
                }
            }
        }

        public bool EnabledOnContextExtendedMenu
        {
            get
            {
                return _fileLocksmithEnabledOnContextExtendedMenu;
            }

            set
            {
                if (value != _fileLocksmithEnabledOnContextExtendedMenu)
                {
                    _fileLocksmithEnabledOnContextExtendedMenu = value;
                    Settings.Properties.ExtendedContextMenuOnly.Value = value;
                    OnPropertyChanged(nameof(EnabledOnContextExtendedMenu));

                    NotifySettingsChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
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

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isFileLocksmithEnabled;
        private bool _fileLocksmithEnabledOnContextExtendedMenu;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsFileLocksmithEnabled));
        }
    }
}
