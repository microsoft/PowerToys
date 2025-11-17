// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerRenameViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private const string ModuleName = PowerRenameSettings.ModuleName;

        private string _settingsConfigFileFolder = string.Empty;

        private PowerRenameSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public PowerRenameViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            try
            {
                PowerRenameLocalProperties localSettings = _settingsUtils.GetSettingsOrDefault<PowerRenameLocalProperties>(GetSettingsSubPath(), "power-rename-settings.json");
                Settings = new PowerRenameSettings(localSettings);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while reading {ModuleName} settings.", e);
#if DEBUG
                if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                {
                    throw;
                }
#endif
                PowerRenameLocalProperties localSettings = new PowerRenameLocalProperties();
                Settings = new PowerRenameSettings(localSettings);
                _settingsUtils.SaveSettings(localSettings.ToJsonString(), GetSettingsSubPath(), "power-rename-settings.json");
            }

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _powerRenameEnabledOnContextMenu = Settings.Properties.ShowIcon.Value;
            _powerRenameEnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
            _powerRenameRestoreFlagsOnLaunch = Settings.Properties.PersistState.Value;
            _powerRenameMaxDispListNumValue = Settings.Properties.MaxMRUSize.Value;
            _autoComplete = Settings.Properties.MRUEnabled.Value;
            _powerRenameUseBoostLib = Settings.Properties.UseBoostLib.Value;

            InitializeEnabledValue();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPowerRenameEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _powerRenameEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _powerRenameEnabled = GeneralSettingsConfig.Enabled.PowerRename;
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _powerRenameEnabled;
        private bool _powerRenameEnabledOnContextMenu;
        private bool _powerRenameEnabledOnContextExtendedMenu;
        private bool _powerRenameRestoreFlagsOnLaunch;
        private int _powerRenameMaxDispListNumValue;
        private bool _autoComplete;
        private bool _powerRenameUseBoostLib;

        public bool IsEnabled
        {
            get
            {
                return _powerRenameEnabled;
            }

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _powerRenameEnabled)
                {
                    GeneralSettingsConfig.Enabled.PowerRename = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());

                    _powerRenameEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    RaisePropertyChanged(nameof(GlobalAndMruEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool MRUEnabled
        {
            get
            {
                return _autoComplete;
            }

            set
            {
                if (value != _autoComplete)
                {
                    _autoComplete = value;
                    Settings.Properties.MRUEnabled.Value = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(GlobalAndMruEnabled));
                }
            }
        }

        public bool GlobalAndMruEnabled
        {
            get
            {
                return _autoComplete && _powerRenameEnabled;
            }
        }

        public bool EnabledOnContextMenu
        {
            get
            {
                return _powerRenameEnabledOnContextMenu;
            }

            set
            {
                if (value != _powerRenameEnabledOnContextMenu)
                {
                    _powerRenameEnabledOnContextMenu = value;
                    Settings.Properties.ShowIcon.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool EnabledOnContextExtendedMenu
        {
            get
            {
                return _powerRenameEnabledOnContextExtendedMenu;
            }

            set
            {
                if (value != _powerRenameEnabledOnContextExtendedMenu)
                {
                    _powerRenameEnabledOnContextExtendedMenu = value;
                    Settings.Properties.ExtendedContextMenuOnly.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool RestoreFlagsOnLaunch
        {
            get
            {
                return _powerRenameRestoreFlagsOnLaunch;
            }

            set
            {
                if (value != _powerRenameRestoreFlagsOnLaunch)
                {
                    _powerRenameRestoreFlagsOnLaunch = value;
                    Settings.Properties.PersistState.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MaxDispListNum
        {
            get
            {
                return _powerRenameMaxDispListNumValue;
            }

            set
            {
                if (value != _powerRenameMaxDispListNumValue)
                {
                    _powerRenameMaxDispListNumValue = value;
                    Settings.Properties.MaxMRUSize.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseBoostLib
        {
            get
            {
                return _powerRenameUseBoostLib;
            }

            set
            {
                if (value != _powerRenameUseBoostLib)
                {
                    _powerRenameUseBoostLib = value;
                    Settings.Properties.UseBoostLib.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (SendConfigMSG != null)
            {
                SndPowerRenameSettings snd = new SndPowerRenameSettings(Settings);
                SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                SendConfigMSG(ipcMessage.ToJsonString());
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(GlobalAndMruEnabled));
        }
    }
}
