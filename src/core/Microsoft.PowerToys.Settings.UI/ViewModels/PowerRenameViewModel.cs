// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerRenameViewModel : Observable
    {
        private const string ModuleName = "PowerRename";

        private PowerRenameSettings Settings { get; set; }

        public PowerRenameViewModel()
        {
            try
            {
                PowerRenameLocalProperties localSettings = SettingsUtils.GetSettings<PowerRenameLocalProperties>(ModuleName, "power-rename-settings.json");
                Settings = new PowerRenameSettings(localSettings);
            }
            catch
            {
                PowerRenameLocalProperties localSettings = new PowerRenameLocalProperties();
                Settings = new PowerRenameSettings(localSettings);
                SettingsUtils.SaveSettings(localSettings.ToJsonString(), ModuleName, "power-rename-settings.json");
            }

            _powerRenameEnabledOnContextMenu = Settings.Properties.ShowIcon.Value;
            _powerRenameEnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
            _powerRenameRestoreFlagsOnLaunch = Settings.Properties.PersistState.Value;
            _powerRenameMaxDispListNumValue = Settings.Properties.MaxMRUSize.Value;
            _autoComplete = Settings.Properties.MRUEnabled.Value;

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

            _powerRenameEnabled = generalSettings.Enabled.PowerRename;
        }

        private bool _powerRenameEnabled = false;
        private bool _powerRenameEnabledOnContextMenu = false;
        private bool _powerRenameEnabledOnContextExtendedMenu = false;
        private bool _powerRenameRestoreFlagsOnLaunch = false;
        private int _powerRenameMaxDispListNumValue = 0;
        private bool _autoComplete = false;

        public bool IsEnabled
        {
            get
            {
                return _powerRenameEnabled;
            }

            set
            {
                if (value != _powerRenameEnabled)
                {
                    if (ShellPage.DefaultSndMSGCallback != null)
                    {
                        GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                        generalSettings.Enabled.PowerRename = value;
                        OutGoingGeneralSettings snd = new OutGoingGeneralSettings(generalSettings);
                        ShellPage.DefaultSndMSGCallback(snd.ToString());

                        _powerRenameEnabled = value;
                        OnPropertyChanged("IsEnabled");
                        RaisePropertyChanged("GlobalAndMruEnabled");
                    }
                }
            }
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
                    RaisePropertyChanged("GlobalAndMruEnabled");
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

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (ShellPage.DefaultSndMSGCallback != null)
            {
                SndPowerRenameSettings snd = new SndPowerRenameSettings(Settings);
                SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
            }
        }
    }
}
