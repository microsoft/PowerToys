// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class ColorPickerViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private readonly ColorPickerSettings _colorPickerSettings;

        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public ColorPickerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            SelectableColorRepresentations = new Dictionary<ColorRepresentationType, string>
            {
                { ColorRepresentationType.CMYK, "CMYK - cmyk(100%, 50%, 75%, 0%)" },
                { ColorRepresentationType.HEX,  "HEX - #FFAA00" },
                { ColorRepresentationType.HSB,  "HSB - hsb(100, 50%, 75%)" },
                { ColorRepresentationType.HSI,  "HSI - hsi(100, 50%, 75%)" },
                { ColorRepresentationType.HSL,  "HSL - hsl(100, 50%, 75%)" },
                { ColorRepresentationType.HSV,  "HSV - hsv(100, 50%, 75%)" },
                { ColorRepresentationType.HWB,  "HWB - hwb(100, 50%, 75%)" },
                { ColorRepresentationType.NCol, "NCol - R10, 50%, 75%" },
                { ColorRepresentationType.RGB,  "RGB - rgb(100, 50, 75)" },
            };

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

        /// <summary>
        /// Gets a list with all selectable <see cref="ColorRepresentationType"/>s
        /// </summary>
        public IReadOnlyDictionary<ColorRepresentationType, string> SelectableColorRepresentations { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    // Set the status of ColorPicker in the general settings
                    GeneralSettingsConfig.Enabled.ColorPicker = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool ChangeCursor
        {
            get => _colorPickerSettings.Properties.ChangeCursor;
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
            get => _colorPickerSettings.Properties.ActivationShortcut;
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

        public ColorRepresentationType SelectedColorRepresentationValue
        {
            get => _colorPickerSettings.Properties.CopiedColorRepresentation;
            set
            {
                if (_colorPickerSettings.Properties.CopiedColorRepresentation != value)
                {
                    _colorPickerSettings.Properties.CopiedColorRepresentation = value;
                    OnPropertyChanged(nameof(SelectedColorRepresentationValue));
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
                       ColorPickerSettings.ModuleName,
                       JsonSerializer.Serialize(_colorPickerSettings)));
        }
    }
}
