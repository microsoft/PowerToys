// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class PowerAccentViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly PowerAccentSettings _powerAccentSettings;

        private readonly ISettingsUtils _settingsUtils;

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

            _isEnabled = GeneralSettingsConfig.Enabled.PowerAccent;
            if (_settingsUtils.SettingsExists(PowerAccentSettings.ModuleName))
            {
                _powerAccentSettings = _settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentSettings.ModuleName);
            }
            else
            {
                _powerAccentSettings = new PowerAccentSettings();
            }

            _inputTimeMs = _powerAccentSettings.Properties.InputTime.Value;

            switch (_powerAccentSettings.Properties.ToolbarPosition.Value)
            {
                case "Top center":
                    _toolbarPositionIndex = 0;
                    break;
                case "Bottom center":
                    _toolbarPositionIndex = 1;
                    break;
                case "Left":
                    _toolbarPositionIndex = 2;
                    break;
                case "Right":
                    _toolbarPositionIndex = 3;
                    break;
                case "Top right corner":
                    _toolbarPositionIndex = 4;
                    break;
                case "Top left corner":
                    _toolbarPositionIndex = 5;
                    break;
                case "Bottom right corner":
                    _toolbarPositionIndex = 6;
                    break;
                case "Bottom left corner":
                    _toolbarPositionIndex = 7;
                    break;
                case "Center":
                    _toolbarPositionIndex = 8;
                    break;
            }

            switch (_powerAccentSettings.Properties.SelectedLang.Value)
            {
                case "ALL":
                    _selectedLangIndex = 0;
                    break;
                case "CUR":
                    _selectedLangIndex = 1;
                    break;
                case "CZ":
                    _selectedLangIndex = 2;
                    break;
                case "DE":
                    _selectedLangIndex = 3;
                    break;
                case "FR":
                    _selectedLangIndex = 4;
                    break;
                case "HU":
                    _selectedLangIndex = 5;
                    break;
                case "IS":
                    _selectedLangIndex = 6;
                    break;
                case "IT":
                    _selectedLangIndex = 7;
                    break;
                case "MI":
                    _selectedLangIndex = 8;
                    break;
                case "PI":
                    _selectedLangIndex = 9;
                    break;
                case "PL":
                    _selectedLangIndex = 10;
                    break;
                case "RO":
                    _selectedLangIndex = 11;
                    break;
                case "SK":
                    _selectedLangIndex = 12;
                    break;
                case "SP":
                    _selectedLangIndex = 13;
                    break;
                case "TK":
                    _selectedLangIndex = 14;
                    break;
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
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

        private int _inputTimeMs = 200;

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
                    switch (_toolbarPositionIndex)
                    {
                        case 0:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Top center";
                            break;

                        case 1:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Bottom center";
                            break;

                        case 2:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Left";
                            break;

                        case 3:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Right";
                            break;

                        case 4:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Top right corner";
                            break;

                        case 5:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Top left corner";
                            break;

                        case 6:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Bottom right corner";
                            break;

                        case 7:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Bottom left corner";
                            break;

                        case 8:
                            _powerAccentSettings.Properties.ToolbarPosition.Value = "Center";
                            break;
                    }

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
                    switch (_selectedLangIndex)
                    {
                        case 0:
                            _powerAccentSettings.Properties.SelectedLang.Value = "ALL";
                            break;
                        case 1:
                            _powerAccentSettings.Properties.SelectedLang.Value = "CUR";
                            break;
                        case 2:
                            _powerAccentSettings.Properties.SelectedLang.Value = "CZ";
                            break;
                        case 3:
                            _powerAccentSettings.Properties.SelectedLang.Value = "DE";
                            break;
                        case 4:
                            _powerAccentSettings.Properties.SelectedLang.Value = "FR";
                            break;
                        case 5:
                            _powerAccentSettings.Properties.SelectedLang.Value = "HU";
                            break;
                        case 6:
                            _powerAccentSettings.Properties.SelectedLang.Value = "IS";
                            break;
                        case 7:
                            _powerAccentSettings.Properties.SelectedLang.Value = "IT";
                            break;
                        case 8:
                            _powerAccentSettings.Properties.SelectedLang.Value = "MI";
                            break;
                        case 9:
                            _powerAccentSettings.Properties.SelectedLang.Value = "PI";
                            break;
                        case 10:
                            _powerAccentSettings.Properties.SelectedLang.Value = "PL";
                            break;
                        case 11:
                            _powerAccentSettings.Properties.SelectedLang.Value = "RO";
                            break;
                        case 12:
                            _powerAccentSettings.Properties.SelectedLang.Value = "SK";
                            break;
                        case 13:
                            _powerAccentSettings.Properties.SelectedLang.Value = "SP";
                            break;
                        case 14:
                            _powerAccentSettings.Properties.SelectedLang.Value = "TK";
                            break;
                    }

                    RaisePropertyChanged(nameof(SelectedLangIndex));
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

        private bool _isEnabled;
    }
}
