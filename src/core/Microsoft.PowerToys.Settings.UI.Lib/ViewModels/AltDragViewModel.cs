// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class AltDragViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private AltDragSettings _altDragSettings;

        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public AltDragViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            if (_settingsUtils.SettingsExists(AltDragSettings.ModuleName))
            {
                _altDragSettings = _settingsUtils.GetSettings<AltDragSettings>(AltDragSettings.ModuleName);
            }
            else
            {
                _altDragSettings = new AltDragSettings();
            }

            _isEnabled = GeneralSettingsConfig.Enabled.ColorPicker;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    // Set the status of ColorPicker in the general settings
                    GeneralSettingsConfig.Enabled.ColorPicker = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public string HotkeyColor
        {
            get
            {
                return _altDragSettings.Properties.HotkeyColor.Value;
            }

            set
            {
                if (_altDragSettings.Properties.HotkeyColor.Value != value)
                {
                    _altDragSettings.Properties.HotkeyColor.Value = value;
                    OnPropertyChanged(nameof(HotkeyColor));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            SendConfigMSG(
                   string.Format("{{ \"powertoys\": {{ \"{0}\": {1} }} }}", ColorPickerSettings.ModuleName, JsonSerializer.Serialize(_altDragSettings)));
        }
    }
}
