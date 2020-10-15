// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class ColorPickerViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private ColorPickerSettings _colorPickerSettings;

        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public ColorPickerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            if (_settingsUtils.SettingsExists(ColorPickerSettings.ModuleName))
            {
                _colorPickerSettings = _settingsUtils.GetSettings<ColorPickerSettings>(ColorPickerSettings.ModuleName);
            }
            else
            {
                _colorPickerSettings = new ColorPickerSettings();
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

        public bool ChangeCursor
        {
            get
            {
                return _colorPickerSettings.Properties.ChangeCursor;
            }

            set
            {
                if (_colorPickerSettings.Properties.ChangeCursor != value)
                {
                    _colorPickerSettings.Properties.ChangeCursor = value;
                    OnPropertyChanged(nameof(ChangeCursor));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get
            {
                return _colorPickerSettings.Properties.ActivationShortcut;
            }

            set
            {
                if (_colorPickerSettings.Properties.ActivationShortcut != value)
                {
                    _colorPickerSettings.Properties.ActivationShortcut = value;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public int CopiedColorRepresentationIndex
        {
            get
            {
                return (int)_colorPickerSettings.Properties.CopiedColorRepresentation;
            }

            set
            {
                if (_colorPickerSettings.Properties.CopiedColorRepresentation != (ColorRepresentationType)value)
                {
                    _colorPickerSettings.Properties.CopiedColorRepresentation = (ColorRepresentationType)value;
                    OnPropertyChanged(nameof(CopiedColorRepresentationIndex));
                    NotifySettingsChanged();
                }
            }
        }

        public bool UseEditor
        {
            get
            {
                return _colorPickerSettings.Properties.UseEditor;
            }

            set
            {
                if (_colorPickerSettings.Properties.UseEditor != value)
                {
                    _colorPickerSettings.Properties.UseEditor = value;
                    OnPropertyChanged(nameof(UseEditor));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            SendConfigMSG(
                   string.Format("{{ \"powertoys\": {{ \"{0}\": {1} }} }}", ColorPickerSettings.ModuleName, JsonSerializer.Serialize(_colorPickerSettings)));
        }
    }
}
