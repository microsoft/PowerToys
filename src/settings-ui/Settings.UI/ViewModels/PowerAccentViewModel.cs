// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerAccentViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly PowerAccentSettings _powerAccentSettings;

        private readonly ISettingsUtils _settingsUtils;

        private const string SpecialGroup = "QuickAccent_Group_Special";
        private const string LanguageGroup = "QuickAccent_Group_Language";

        public List<PowerAccentLanguageModel> Languages { get; } = [
            new PowerAccentLanguageModel("SPECIAL", "QuickAccent_SelectedLanguage_Special", SpecialGroup),
            new PowerAccentLanguageModel("BG", "QuickAccent_SelectedLanguage_Bulgarian", LanguageGroup),
            new PowerAccentLanguageModel("CA", "QuickAccent_SelectedLanguage_Catalan", LanguageGroup),
            new PowerAccentLanguageModel("CRH", "QuickAccent_SelectedLanguage_Crimean", LanguageGroup),
            new PowerAccentLanguageModel("CUR", "QuickAccent_SelectedLanguage_Currency", SpecialGroup),
            new PowerAccentLanguageModel("HR", "QuickAccent_SelectedLanguage_Croatian", LanguageGroup),
            new PowerAccentLanguageModel("CZ", "QuickAccent_SelectedLanguage_Czech", LanguageGroup),
            new PowerAccentLanguageModel("DK", "QuickAccent_SelectedLanguage_Danish", LanguageGroup),
            new PowerAccentLanguageModel("GA", "QuickAccent_SelectedLanguage_Gaeilge", LanguageGroup),
            new PowerAccentLanguageModel("GD", "QuickAccent_SelectedLanguage_Gaidhlig", LanguageGroup),
            new PowerAccentLanguageModel("NL", "QuickAccent_SelectedLanguage_Dutch", LanguageGroup),
            new PowerAccentLanguageModel("EL", "QuickAccent_SelectedLanguage_Greek", LanguageGroup),
            new PowerAccentLanguageModel("EST", "QuickAccent_SelectedLanguage_Estonian", LanguageGroup),
            new PowerAccentLanguageModel("EPO", "QuickAccent_SelectedLanguage_Esperanto", LanguageGroup),
            new PowerAccentLanguageModel("FI", "QuickAccent_SelectedLanguage_Finnish", LanguageGroup),
            new PowerAccentLanguageModel("FR", "QuickAccent_SelectedLanguage_French", LanguageGroup),
            new PowerAccentLanguageModel("DE", "QuickAccent_SelectedLanguage_German", LanguageGroup),
            new PowerAccentLanguageModel("HE", "QuickAccent_SelectedLanguage_Hebrew", LanguageGroup),
            new PowerAccentLanguageModel("HU", "QuickAccent_SelectedLanguage_Hungarian", LanguageGroup),
            new PowerAccentLanguageModel("IS", "QuickAccent_SelectedLanguage_Icelandic", LanguageGroup),
            new PowerAccentLanguageModel("IPA", "QuickAccent_SelectedLanguage_IPA", SpecialGroup),
            new PowerAccentLanguageModel("IT", "QuickAccent_SelectedLanguage_Italian", LanguageGroup),
            new PowerAccentLanguageModel("KU", "QuickAccent_SelectedLanguage_Kurdish", LanguageGroup),
            new PowerAccentLanguageModel("LT", "QuickAccent_SelectedLanguage_Lithuanian", LanguageGroup),
            new PowerAccentLanguageModel("MK", "QuickAccent_SelectedLanguage_Macedonian", LanguageGroup),
            new PowerAccentLanguageModel("MI", "QuickAccent_SelectedLanguage_Maori", LanguageGroup),
            new PowerAccentLanguageModel("NO", "QuickAccent_SelectedLanguage_Norwegian", LanguageGroup),
            new PowerAccentLanguageModel("PI", "QuickAccent_SelectedLanguage_Pinyin", LanguageGroup),
            new PowerAccentLanguageModel("PIE", "QuickAccent_SelectedLanguage_Proto_Indo_European", LanguageGroup),
            new PowerAccentLanguageModel("PL", "QuickAccent_SelectedLanguage_Polish", LanguageGroup),
            new PowerAccentLanguageModel("PT", "QuickAccent_SelectedLanguage_Portuguese", LanguageGroup),
            new PowerAccentLanguageModel("RO", "QuickAccent_SelectedLanguage_Romanian", LanguageGroup),
            new PowerAccentLanguageModel("ROM", "QuickAccent_SelectedLanguage_Romanization", SpecialGroup),
            new PowerAccentLanguageModel("SK", "QuickAccent_SelectedLanguage_Slovak", LanguageGroup),
            new PowerAccentLanguageModel("SL", "QuickAccent_SelectedLanguage_Slovenian", LanguageGroup),
            new PowerAccentLanguageModel("SP", "QuickAccent_SelectedLanguage_Spanish", LanguageGroup),
            new PowerAccentLanguageModel("SR", "QuickAccent_SelectedLanguage_Serbian", LanguageGroup),
            new PowerAccentLanguageModel("SR_CYRL", "QuickAccent_SelectedLanguage_Serbian_Cyrillic", LanguageGroup),
            new PowerAccentLanguageModel("SV", "QuickAccent_SelectedLanguage_Swedish", LanguageGroup),
            new PowerAccentLanguageModel("TK", "QuickAccent_SelectedLanguage_Turkish", LanguageGroup),
            new PowerAccentLanguageModel("CY", "QuickAccent_SelectedLanguage_Welsh", LanguageGroup),
        ];

        public PowerAccentLanguageGroupModel[] LanguageGroups { get; private set; }

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
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();
            InitializeLanguages();

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

            if (!string.IsNullOrWhiteSpace(_powerAccentSettings.Properties.SelectedLang.Value) && !_powerAccentSettings.Properties.SelectedLang.Value.Contains("ALL"))
            {
                SelectedLanguageOptions = _powerAccentSettings.Properties.SelectedLang.Value.Split(',')
                   .Select(l => Languages.Find(lang => lang.LanguageCode == l))
                   .Where(l => l != null) // Wrongly typed languages will appear as null after find. We want to remove those to avoid crashes.
                   .ToArray();
            }
            else if (_powerAccentSettings.Properties.SelectedLang.Value.Contains("ALL"))
            {
                SelectedLanguageOptions = Languages.ToArray();
            }
            else
            {
                SelectedLanguageOptions = Array.Empty<PowerAccentLanguageModel>();
            }

            _toolbarPositionIndex = Array.IndexOf(_toolbarOptions, _powerAccentSettings.Properties.ToolbarPosition.Value);

            // set the callback functions value to handle outgoing IPC message.
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

        /// <summary>
        /// Adds Localized Language Name, sorts by it and splits languages into two groups.
        /// </summary>
        private void InitializeLanguages()
        {
            foreach (var item in Languages)
            {
                item.Language = ResourceLoaderInstance.ResourceLoader.GetString(item.LanguageResourceID);
            }

            Languages.Sort((x, y) => string.Compare(x.Language, y.Language, StringComparison.Ordinal));
            LanguageGroups = Languages
                .GroupBy(language => language.GroupResourceID)
                .Select(grp => new PowerAccentLanguageGroupModel(grp.ToList(), ResourceLoaderInstance.ResourceLoader.GetString(grp.Key)))
                .ToArray();
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

        public bool DoNotActivateOnGameMode
        {
            get
            {
                return _powerAccentSettings.Properties.DoNotActivateOnGameMode;
            }

            set
            {
                if (value != _powerAccentSettings.Properties.DoNotActivateOnGameMode)
                {
                    _powerAccentSettings.Properties.DoNotActivateOnGameMode = value;
                    OnPropertyChanged(nameof(DoNotActivateOnGameMode));
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

        public bool AllSelected => _selectedLanguageOptions.Length == Languages.Count;

        private PowerAccentLanguageModel[] _selectedLanguageOptions;

        public PowerAccentLanguageModel[] SelectedLanguageOptions
        {
            get => _selectedLanguageOptions;
            set
            {
                _selectedLanguageOptions = value;
                _powerAccentSettings.Properties.SelectedLang.Value = string.Join(',', _selectedLanguageOptions.Select(l => l.LanguageCode));
                RaisePropertyChanged(nameof(SelectedLanguageOptions));
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
