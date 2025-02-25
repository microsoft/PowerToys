// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ShortcutGuideViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private ShortcutGuideSettings Settings { get; set; }

        private const string ModuleName = ShortcutGuideSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        private string _settingsConfigFileFolder = string.Empty;
        private string _disabledApps;

        public ShortcutGuideViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<ShortcutGuideSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            SettingsUtils = settingsUtils;

            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            // To obtain the general PowerToys settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // To obtain the shortcut guide settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            InitializeEnabledValue();

            _useLegacyPressWinKeyBehavior = Settings.Properties.UseLegacyPressWinKeyBehavior.Value;
            _pressTimeForGlobalWindowsShortcuts = Settings.Properties.PressTimeForGlobalWindowsShortcuts.Value;
            _pressTimeForTaskbarIconShortcuts = Settings.Properties.PressTimeForTaskbarIconShortcuts.Value;
            _opacity = Settings.Properties.OverlayOpacity.Value;
            _disabledApps = Settings.Properties.DisabledApps.Value;

            switch (Settings.Properties.Theme.Value)
            {
                case "dark": _themeIndex = 0; break;
                case "light": _themeIndex = 1; break;
                case "system": _themeIndex = 2; break;
            }
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ShortcutGuide;
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private int _themeIndex;
        private bool _useLegacyPressWinKeyBehavior;
        private int _pressTimeForGlobalWindowsShortcuts;
        private int _pressTimeForTaskbarIconShortcuts;
        private int _opacity;

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // To update the status of shortcut guide in General PowerToy settings.
                    GeneralSettingsConfig.Enabled.ShortcutGuide = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public HotkeySettings OpenShortcutGuide
        {
            get
            {
                return Settings.Properties.OpenShortcutGuide;
            }

            set
            {
                if (Settings.Properties.OpenShortcutGuide != value)
                {
                    Settings.Properties.OpenShortcutGuide = value ?? Settings.Properties.DefaultOpenShortcutGuide;
                    NotifyPropertyChanged();
                }
            }
        }

        public int ThemeIndex
        {
            get
            {
                return _themeIndex;
            }

            set
            {
                if (_themeIndex != value)
                {
                    switch (value)
                    {
                        case 0: Settings.Properties.Theme.Value = "dark"; break;
                        case 1: Settings.Properties.Theme.Value = "light"; break;
                        case 2: Settings.Properties.Theme.Value = "system"; break;
                    }

                    _themeIndex = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int OverlayOpacity
        {
            get
            {
                return _opacity;
            }

            set
            {
                if (_opacity != value)
                {
                    _opacity = value;
                    Settings.Properties.OverlayOpacity.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool UseLegacyPressWinKeyBehavior
        {
            get
            {
                return _useLegacyPressWinKeyBehavior;
            }

            set
            {
                if (_useLegacyPressWinKeyBehavior != value)
                {
                    _useLegacyPressWinKeyBehavior = value;
                    Settings.Properties.UseLegacyPressWinKeyBehavior.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int PressTime
        {
            get
            {
                return _pressTimeForGlobalWindowsShortcuts;
            }

            set
            {
                if (_pressTimeForGlobalWindowsShortcuts != value)
                {
                    _pressTimeForGlobalWindowsShortcuts = value;
                    Settings.Properties.PressTimeForGlobalWindowsShortcuts.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int DelayTime
        {
            get
            {
                return _pressTimeForTaskbarIconShortcuts;
            }

            set
            {
                if (_pressTimeForTaskbarIconShortcuts != value)
                {
                    _pressTimeForTaskbarIconShortcuts = value;
                    Settings.Properties.PressTimeForTaskbarIconShortcuts.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string DisabledApps
        {
            get
            {
                return _disabledApps;
            }

            set
            {
                if (value != _disabledApps)
                {
                    _disabledApps = value;
                    Settings.Properties.DisabledApps.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndShortcutGuideSettings outsettings = new SndShortcutGuideSettings(Settings);
            SndModuleSettings<SndShortcutGuideSettings> ipcMessage = new SndModuleSettings<SndShortcutGuideSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
