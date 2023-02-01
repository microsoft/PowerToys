// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerAccentViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly PowerAccentSettings _powerAccentSettings;

        private readonly ISettingsUtils _settingsUtils;

        // These should be in the same order as the ComboBoxItems in PowerAccentPage.xaml
        private readonly string[] _languageOptions =
        {
            "ALL",
            "CA",
            "CUR",
            "HR",
            "CZ",
            "GA",
            "GD",
            "NL",
            "EST",
            "FR",
            "DE",
            "HE",
            "HU",
            "IS",
            "IT",
            "KU",
            "MK",
            "MI",
            "NO",
            "PI",
            "PL",
            "PT",
            "RO",
            "SK",
            "SP",
            "SR",
            "SV",
            "TK",
            "CY",
        };

        private readonly string[] _toolbarOptions =
        {
            "Top center",
            "Bottom center",
            "Left",
            "Right",
            "Top right corner",
            "Top left corner",
            "Bottom right corner",
            "Bottom left corner",
            "Center",
        };

        private Func<string, int> SendConfigMSG { get; }

        public PowerAccentViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            if (_settingsUtils.SettingsExists(PowerAccentSettings.ModuleName))
            {
                _powerAccentSettings = _settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentSettings.ModuleName);
            }
            else
            {
                _powerAccentSettings = new PowerAccentSettings();
            }

            _inputTimeMs = _powerAccentSettings.Properties.InputTime.Value;

            _excludedApps = _powerAccentSettings.Properties.ExcludedApps.Value;

            _selectedLangIndex = Array.IndexOf(_languageOptions, _powerAccentSettings.Properties.SelectedLang.Value);

            _toolbarPositionIndex = Array.IndexOf(_toolbarOptions, _powerAccentSettings.Properties.ToolbarPosition.Value);

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredQuickAccentEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.PowerAccent;
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

                    GeneralSettingsConfig.Enabled.PowerAccent = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public int ActivationKey
        {
            get
            {
                return (int)_powerAccentSettings.Properties.ActivationKey;
            }

            set
            {
                if (value != (int)_powerAccentSettings.Properties.ActivationKey)
                {
                    _powerAccentSettings.Properties.ActivationKey = (PowerAccentActivationKey)value;
                    OnPropertyChanged(nameof(ActivationKey));
                    RaisePropertyChanged();
                }
            }
        }

        private int _inputTimeMs = PowerAccentSettings.DefaultInputTimeMs;

        public int InputTimeMs
        {
            get
            {
                return _inputTimeMs;
            }

            set
            {
                if (value != _inputTimeMs)
                {
                    _inputTimeMs = value;
                    _powerAccentSettings.Properties.InputTime.Value = value;
                    OnPropertyChanged(nameof(InputTimeMs));
                    RaisePropertyChanged();
                }
            }
        }

        private string _excludedApps;

        public string ExcludedApps
        {
            get
            {
                return _excludedApps;
            }

            set
            {
                if (value != _excludedApps)
                {
                    _excludedApps = value;
                    _powerAccentSettings.Properties.ExcludedApps.Value = value;
                    OnPropertyChanged(nameof(ExcludedApps));
                    RaisePropertyChanged();
                }
            }
        }

        private int _toolbarPositionIndex;

        public int ToolbarPositionIndex
        {
            get
            {
                return _toolbarPositionIndex;
            }

            set
            {
                if (_toolbarPositionIndex != value)
                {
                    _toolbarPositionIndex = value;
                    _powerAccentSettings.Properties.ToolbarPosition.Value = _toolbarOptions[value];
                    RaisePropertyChanged(nameof(ToolbarPositionIndex));
                }
            }
        }

        private int _selectedLangIndex;

        public int SelectedLangIndex
        {
            get
            {
                return _selectedLangIndex;
            }

            set
            {
                if (_selectedLangIndex != value)
                {
                    _selectedLangIndex = value;
                    _powerAccentSettings.Properties.SelectedLang.Value = _languageOptions[value];
                    RaisePropertyChanged(nameof(SelectedLangIndex));
                }
            }
        }

        public bool ShowUnicodeDescription
        {
            get
            {
                return _powerAccentSettings.Properties.ShowUnicodeDescription;
            }

            set
            {
                if (value != _powerAccentSettings.Properties.ShowUnicodeDescription)
                {
                    _powerAccentSettings.Properties.ShowUnicodeDescription = value;
                    OnPropertyChanged(nameof(ShowUnicodeDescription));
                    RaisePropertyChanged();
                }
            }
        }

        public bool SortByUsageFrequency
        {
            get
            {
                return _powerAccentSettings.Properties.SortByUsageFrequency;
            }

            set
            {
                if (value != _powerAccentSettings.Properties.SortByUsageFrequency)
                {
                    _powerAccentSettings.Properties.SortByUsageFrequency = value;
                    OnPropertyChanged(nameof(SortByUsageFrequency));
                    RaisePropertyChanged();
                }
            }
        }

        public bool StartSelectionFromTheLeft
        {
            get
            {
                return _powerAccentSettings.Properties.StartSelectionFromTheLeft;
            }

            set
            {
                if (value != _powerAccentSettings.Properties.StartSelectionFromTheLeft)
                {
                    _powerAccentSettings.Properties.StartSelectionFromTheLeft = value;
                    OnPropertyChanged(nameof(StartSelectionFromTheLeft));
                    RaisePropertyChanged();
                }
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (SendConfigMSG != null)
            {
                SndPowerAccentSettings snd = new SndPowerAccentSettings(_powerAccentSettings);
                SndModuleSettings<SndPowerAccentSettings> ipcMessage = new SndModuleSettings<SndPowerAccentSettings>(snd);
                SendConfigMSG(ipcMessage.ToJsonString());
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
    }
}
