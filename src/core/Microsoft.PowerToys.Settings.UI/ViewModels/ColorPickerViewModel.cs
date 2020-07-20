// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ColorPickerViewModel : Observable
    {
        private ColorPickerSettings _colorPickerSettings;
        private bool _isEnabled;

        public ColorPickerViewModel()
        {
            if (SettingsUtils.SettingsExists(ColorPickerSettings.ModuleName))
            {
                _colorPickerSettings = SettingsUtils.GetSettings<ColorPickerSettings>(ColorPickerSettings.ModuleName);
            }
            else
            {
                _colorPickerSettings = new ColorPickerSettings();
            }

            if (SettingsUtils.SettingsExists())
            {
                var generalSettings = SettingsUtils.GetSettings<GeneralSettings>();
                _isEnabled = generalSettings.Enabled.ColorPicker;
            }
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

                    // grab the latest version of settings
                    var generalSettings = SettingsUtils.GetSettings<GeneralSettings>();
                    generalSettings.Enabled.ColorPicker = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(generalSettings);
                    ShellPage.DefaultSndMSGCallback(outgoing.ToString());
                }
            }
        }

        public bool ChangeCursor
        {
            get
            {
                return _colorPickerSettings.properties.ChangeCursor;
            }

            set
            {
                if (_colorPickerSettings.properties.ChangeCursor != value)
                {
                    _colorPickerSettings.properties.ChangeCursor = value;
                    OnPropertyChanged(nameof(ChangeCursor));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get
            {
                return _colorPickerSettings.properties.ActivationShortcut;
            }

            set
            {
                if (_colorPickerSettings.properties.ActivationShortcut != value)
                {
                    _colorPickerSettings.properties.ActivationShortcut = value;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public int CopiedColorRepresentationIndex
        {
            get
            {
                return (int)_colorPickerSettings.properties.CopiedColorRepresentation;
            }

            set
            {
                if (_colorPickerSettings.properties.CopiedColorRepresentation != (ColorRepresentationType)value)
                {
                    _colorPickerSettings.properties.CopiedColorRepresentation = (ColorRepresentationType)value;
                    OnPropertyChanged(nameof(CopiedColorRepresentationIndex));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            ShellPage.DefaultSndMSGCallback(
                   string.Format("{{ \"powertoys\": {{ \"{0}\": {1} }} }}", ColorPickerSettings.ModuleName, JsonSerializer.Serialize(_colorPickerSettings)));
        }
    }
}
