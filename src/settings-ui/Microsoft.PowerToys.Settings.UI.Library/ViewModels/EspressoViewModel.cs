// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class EspressoViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private EspressoSettings Settings { get; set; }

        private string settingsConfigFileFolder = string.Empty;

        private Func<string, int> SendConfigMSG { get; }

        public EspressoViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<EspressoSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            settingsConfigFileFolder = configFileSubfolder;

            // To obtain the settings configurations of Fancy zones.
            if (moduleSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(moduleSettingsRepository));
            }

            Settings = moduleSettingsRepository.SettingsConfig;

            _keepDisplayOn = Settings.Properties.KeepDisplayOn.Value;
            _mode = Settings.Properties.Mode;
            _timeAllocation = Settings.Properties.TimeAllocation.Value;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _isEnabled = GeneralSettingsConfig.Enabled.Espresso;
        }

        private bool _isEnabled;
        private bool _keepDisplayOn;
        private EspressoMode _mode;
        private int _timeAllocation;
    }
}
