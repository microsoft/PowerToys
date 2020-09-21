// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class ShortcutGuideViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private ShortcutGuideSettings Settings { get; set; }

        private const string ModuleName = "Shortcut Guide";

        private Func<string, int> SendConfigMSG { get; }

        private string _settingsConfigFileFolder = string.Empty;

        public ShortcutGuideViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<ShortcutGuideSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // To obtain the general PowerToys settings.
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // To obtain the shortcut guide settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _isEnabled = GeneralSettingsConfig.Enabled.ShortcutGuide;
            _pressTime = Settings.Properties.PressTime.Value;
            _opacity = Settings.Properties.OverlayOpacity.Value;

            string theme = Settings.Properties.Theme.Value;

            if (theme == "dark")
            {
                _themeIndex = 0;
            }

            if (theme == "light")
            {
                _themeIndex = 1;
            }

            if (theme == "system")
            {
                _themeIndex = 2;
            }
        }

        private bool _isEnabled = false;
        private int _themeIndex = 0;
        private int _pressTime = 0;
        private int _opacity = 0;

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // To update the status of shortcut guide in General PowerToy settings.
                    GeneralSettingsConfig.Enabled.ShortcutGuide = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged("IsEnabled");
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
                    if (value == 0)
                    {
                        // set theme to dark.
                        Settings.Properties.Theme.Value = "dark";
                        _themeIndex = value;
                        RaisePropertyChanged();
                    }

                    if (value == 1)
                    {
                        // set theme to light.
                        Settings.Properties.Theme.Value = "light";
                        _themeIndex = value;
                        RaisePropertyChanged();
                    }

                    if (value == 2)
                    {
                        // set theme to system default.
                        Settings.Properties.Theme.Value = "system";
                        _themeIndex = value;
                        RaisePropertyChanged();
                    }
                }
            }
        }

        public int PressTime
        {
            get
            {
                return _pressTime;
            }

            set
            {
                if (_pressTime != value)
                {
                    _pressTime = value;
                    Settings.Properties.PressTime.Value = value;
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SndShortcutGuideSettings outsettings = new SndShortcutGuideSettings(Settings);
            SndModuleSettings<SndShortcutGuideSettings> ipcMessage = new SndModuleSettings<SndShortcutGuideSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
        }
    }
}
