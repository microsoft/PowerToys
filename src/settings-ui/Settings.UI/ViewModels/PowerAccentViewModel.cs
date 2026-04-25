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
using PowerAccent.Common;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerAccentViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly PowerAccentSettings _powerAccentSettings;

        private readonly SettingsUtils _settingsUtils;

        /// <summary>
        /// Maps each <see cref="LanguageGroup"/> to its resx resource key so that group
        /// header strings can be looked up by the Settings UI. Every value defined in
        /// <see cref="LanguageGroup"/> must have an entry here.
        /// </summary>
        private static readonly Dictionary<LanguageGroup, string> _groupResourceKeys = new()
        {
            [LanguageGroup.Language] = "QuickAccent_Group_Language",
            [LanguageGroup.Special] = "QuickAccent_Group_Special",
            [LanguageGroup.UserDefined] = "QuickAccent_Group_UserDefined",
        };

        /// <summary>
        /// Gets the flat list of all available languages, derived from
        /// <see cref="CharacterMappings.All"/>. In the Settings UI, this list is sorted
        /// alphabetically by the localized display name and arranged into groups based on
        /// the <see cref="LanguageGroup"/>. Populated by <see cref="InitializeLanguages"/>.
        /// </summary>
        public List<PowerAccentLanguageModel> Languages { get; private set; }

        /// <summary>
        /// Gets the languages arranged into display groups, in the order defined by
        /// <see cref="CharacterMappings.GroupDisplayOrder"/>. Bound to the Settings UI
        /// list.
        /// </summary>
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

        public PowerAccentViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
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

            var selectedLangEntries = _powerAccentSettings.Properties.SelectedLang.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToArray();

            if (selectedLangEntries.Any(l => l.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
            {
                SelectedLanguageOptions = Languages.ToArray();
            }
            else if (selectedLangEntries.Length > 0)
            {
                SelectedLanguageOptions = selectedLangEntries
                    .Select(l => Languages.Find(lang => lang.LanguageCode.Equals(l, StringComparison.OrdinalIgnoreCase)))
                    .Where(l => l != null) // Unrecognized language codes (e.g. manual edits, removed languages) are skipped.
                    .ToArray();
            }
            else
            {
                SelectedLanguageOptions = [];
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
        /// Builds the Settings UI language models. This resolves localized display names
        /// for each language, sorts by name within each group, and arranges groups in the
        /// order defined by <see cref="CharacterMappings.GroupDisplayOrder"/>. The
        /// resulting list of languages and groups is stored in the
        /// <see cref="Languages"/> and <see cref="LanguageGroups"/> properties, which are
        /// bound to the Settings UI.
        /// </summary>
        private void InitializeLanguages()
        {
            // Build the flat list and resolve localized display names.
            Languages = CharacterMappings.All.Select(lang =>
            {
                string languageResourceId = $"QuickAccent_SelectedLanguage_{lang.Identifier}";

                var model = new PowerAccentLanguageModel(
                    lang.Id.ToString(),
                    languageResourceId,
                    _groupResourceKeys[lang.Group]);

                model.Language = ResourceLoaderInstance.ResourceLoader.GetString(languageResourceId);
                return model;
            }).ToList();

            // Sort the flat list alphabetically by the localized display name.
            Languages.Sort((x, y) => string.Compare(x.Language, y.Language, StringComparison.Ordinal));

            // Group them in the explicit order defined by the core library. Note:
            // PowerAccentLanguageModel does not hold a direct dependency on the
            // LanguageGroup enum. Instead, we use the stable GroupResourceID as a
            // decoupled key to map the core groups to the Settings UI models.
            LanguageGroups = CharacterMappings.GroupDisplayOrder
                .Select(group =>
                {
                    string groupResourceId = _groupResourceKeys[group];
                    var groupedLanguages = Languages.Where(lang => lang.GroupResourceID == groupResourceId).ToList();

                    return groupedLanguages.Count > 0
                        ? new PowerAccentLanguageGroupModel(groupedLanguages, ResourceLoaderInstance.ResourceLoader.GetString(groupResourceId))
                        : null; // Skip groups with no languages.
                })
                .OfType<PowerAccentLanguageGroupModel>()
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
