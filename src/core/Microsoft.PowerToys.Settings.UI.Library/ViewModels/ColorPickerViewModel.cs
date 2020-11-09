// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class ColorPickerViewModel : Observable
    {
        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it, otherwise we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly object _delayedActionLock = new object();

        private ColorPickerSettings _colorPickerSettings;
        private DispatcherTimer _delayedTimer;

        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public ColorPickerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

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

            _delayedTimer = new DispatcherTimer();
            _delayedTimer.Interval = new TimeSpan(0, 0, 0, 0, SaveSettingsDelayInMs);
            _delayedTimer.Tick += DelayedTimer_Tick;

            InitializeColorFormats();
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

        public ObservableCollection<ColorFormatModel> ColorFormats { get; } = new ObservableCollection<ColorFormatModel>();

        private void InitializeColorFormats()
        {
            var visibleFormats = _colorPickerSettings.Properties.VisibleColorFormats;
            var formatsUnordered = new List<ColorFormatModel>();

            var hexFormatName = ColorRepresentationType.HEX.ToString();
            var rgbFormatName = ColorRepresentationType.RGB.ToString();
            var hslFormatName = ColorRepresentationType.HSL.ToString();
            var hsvFormatName = ColorRepresentationType.HSV.ToString();
            var cmykFormatName = ColorRepresentationType.CMYK.ToString();

            formatsUnordered.Add(new ColorFormatModel(hexFormatName, "#EF68FF", visibleFormats.ContainsKey(hexFormatName) && visibleFormats[hexFormatName]));
            formatsUnordered.Add(new ColorFormatModel(rgbFormatName, "rgb(239, 104, 255)", visibleFormats.ContainsKey(rgbFormatName) && visibleFormats[rgbFormatName]));
            formatsUnordered.Add(new ColorFormatModel(hslFormatName, "hsl(294, 100%, 70%)", visibleFormats.ContainsKey(hslFormatName) && visibleFormats[hslFormatName]));
            formatsUnordered.Add(new ColorFormatModel(hsvFormatName, "hsv(294, 59%, 100%)", visibleFormats.ContainsKey(hsvFormatName) && visibleFormats[hsvFormatName]));
            formatsUnordered.Add(new ColorFormatModel(cmykFormatName, "cmyk(6%, 59%, 0%, 0%)", visibleFormats.ContainsKey(cmykFormatName) && visibleFormats[cmykFormatName]));

            foreach (var storedColorFormat in _colorPickerSettings.Properties.VisibleColorFormats)
            {
                var predefinedFormat = formatsUnordered.FirstOrDefault(it => it.Name == storedColorFormat.Key);
                if (predefinedFormat != null)
                {
                    predefinedFormat.PropertyChanged += ColorFormat_PropertyChanged;
                    ColorFormats.Add(predefinedFormat);
                    formatsUnordered.Remove(predefinedFormat);
                }
            }

            // settings file might not have all formats listed, add remaining ones we support
            foreach (var remainingColorFormat in formatsUnordered)
            {
                remainingColorFormat.PropertyChanged += ColorFormat_PropertyChanged;
                ColorFormats.Add(remainingColorFormat);
            }

            ColorFormats.CollectionChanged += ColorFormats_CollectionChanged;
        }

        private void ColorFormats_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateColorFormats();
            ScheduleSavingOfSettings();
        }

        private void ColorFormat_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateColorFormats();
            ScheduleSavingOfSettings();
        }

        private void ScheduleSavingOfSettings()
        {
            lock (_delayedActionLock)
            {
                if (_delayedTimer.IsEnabled)
                {
                    _delayedTimer.Stop();
                }

                _delayedTimer.Start();
            }
        }

        private void DelayedTimer_Tick(object sender, EventArgs e)
        {
            lock (_delayedActionLock)
            {
                _delayedTimer.Stop();
                NotifySettingsChanged();
            }
        }

        private void UpdateColorFormats()
        {
            _colorPickerSettings.Properties.VisibleColorFormats.Clear();
            foreach (var colorFormat in ColorFormats)
            {
                _colorPickerSettings.Properties.VisibleColorFormats.Add(colorFormat.Name, colorFormat.IsShown);
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
