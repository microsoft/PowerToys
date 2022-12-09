// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class PeekViewModel : Observable
    {
        private bool _isEnabled;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly PeekSettings _peekSettings;

        private Func<string, int> SendConfigMSG { get; }

        public PeekViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            if (_settingsUtils.SettingsExists(ColorPickerSettings.ModuleName))
            {
                _peekSettings = _settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekSettings.ModuleName);
            }
            else
            {
                _peekSettings = new PeekSettings();
            }

            _isEnabled = GeneralSettingsConfig.Enabled.Peek;

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

                    GeneralSettingsConfig.Enabled.Peek = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get => _peekSettings.Properties.ActivationShortcut;
            set
            {
                if (_peekSettings.Properties.ActivationShortcut != value)
                {
                    _peekSettings.Properties.ActivationShortcut = value;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public double UnsupportedFileWidthPercent
        {
            get => _peekSettings.Properties.UnsupportedFileWidthPercent;
            set
            {
                if (_peekSettings.Properties.UnsupportedFileWidthPercent != value)
                {
                    _peekSettings.Properties.UnsupportedFileWidthPercent = value;
                    OnPropertyChanged(nameof(UnsupportedFileWidthPercent));
                    NotifySettingsChanged();
                }
            }
        }

        public double UnsupportedFileHeightPercent
        {
            get => _peekSettings.Properties.UnsupportedFileHeightPercent;

            set
            {
                if (_peekSettings.Properties.UnsupportedFileHeightPercent != value)
                {
                    _peekSettings.Properties.UnsupportedFileHeightPercent = value;
                    OnPropertyChanged(nameof(UnsupportedFileHeightPercent));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       PeekSettings.ModuleName,
                       JsonSerializer.Serialize(_peekSettings)));
        }
    }
}
