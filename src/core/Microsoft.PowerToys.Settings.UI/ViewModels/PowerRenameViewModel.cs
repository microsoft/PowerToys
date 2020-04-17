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
                Settings = SettingsUtils.GetSettings<PowerRenameSettings>(ModuleName);
            }
            catch
            {
                Settings = new PowerRenameSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            _powerRenameEnabled = Settings.properties.MruEnabled.Value;
            _powerRenameEnabledOnContextMenu = Settings.properties.ShowIconInMenu.Value;
            _powerRenameEnabledOnContextExtendedMenu = Settings.properties.ShowExtendedMenu.Value;
            _powerRenameRestoreFlagsOnLaunch = Settings.properties.PersistInput.Value;
            _powerRenameMaxDispListNumValue = Settings.properties.MaxMruSize.Value;
        }

        private bool _powerRenameEnabled = false;
        private bool _powerRenameEnabledOnContextMenu = false;
        private bool _powerRenameEnabledOnContextExtendedMenu = false;
        private bool _powerRenameRestoreFlagsOnLaunch = false;
        private int _powerRenameMaxDispListNumValue = 0;

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
                    _powerRenameEnabled = value;
                    Settings.properties.MruEnabled.Value = value;
                    RaisePropertyChanged();
                }
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
                    Settings.properties.ShowIconInMenu.Value = value;
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
                    Settings.properties.ShowExtendedMenu.Value = value;
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
                    Settings.properties.PersistInput.Value = value;
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
                    Settings.properties.MaxMruSize.Value = value;
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