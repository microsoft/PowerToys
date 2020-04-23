// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ShortcutGuideViewModel : Observable
    {
        private ShortcutGuideSettings Settings { get; set; }

        private const string ModuleName = "Shortcut Guide";

        public ShortcutGuideViewModel()
        {
            try
            {
                Settings = SettingsUtils.GetSettings<ShortcutGuideSettings>(ModuleName);
            }
            catch
            {
                Settings = new ShortcutGuideSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            GeneralSettings generalSettings;

            try
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            catch
            {
                generalSettings = new GeneralSettings();
                SettingsUtils.SaveSettings(generalSettings.ToJsonString(), string.Empty);
            }

            this._isEnabled = generalSettings.Enabled.ShortcutGuide;
            this._pressTime = Settings.Properties.PressTime.Value;
            this._opacity = Settings.Properties.OverlayOpacity.Value;

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
                    GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                    generalSettings.Enabled.ShortcutGuide = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(generalSettings);
                    ShellPage.DefaultSndMSGCallback(snd.ToString());
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

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SndShortcutGuideSettings outsettings = new SndShortcutGuideSettings(Settings);
            SndModuleSettings<SndShortcutGuideSettings> ipcMessage = new SndModuleSettings<SndShortcutGuideSettings>(outsettings);
            ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
        }
    }
}
