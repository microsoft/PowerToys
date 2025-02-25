// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Timers;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ColorPickerViewModel : Observable, IDisposable
    {
        private bool disposedValue;

        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it, otherwise we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly System.Threading.Lock _delayedActionLock = new System.Threading.Lock();

        private readonly ColorPickerSettings _colorPickerSettings;
        private Timer _delayedTimer;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private int _colorFormatPreviewIndex;

        private Func<string, int> SendConfigMSG { get; }

        private Dictionary<string, string> _colorFormatsPreview;

        public ColorPickerViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<ColorPickerSettings> colorPickerSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            _colorPickerSettings = colorPickerSettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;

            InitializeColorFormats();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredColorPickerEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ColorPicker;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

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

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
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
                    _colorPickerSettings.Properties.ActivationShortcut = value ?? _colorPickerSettings.Properties.DefaultActivationShortcut;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public string SelectedColorRepresentationValue
        {
            get => _colorPickerSettings.Properties.CopiedColorRepresentation;
            set
            {
                if (value == null)
                {
                    return; // do not set null value, it occurs when the combobox itemSource gets modified. Right after it well be reset to the correct value
                }

                if (_colorPickerSettings.Properties.CopiedColorRepresentation != value)
                {
                    _colorPickerSettings.Properties.CopiedColorRepresentation = value;
                    OnPropertyChanged(nameof(SelectedColorRepresentationValue));
                    NotifySettingsChanged();
                }
            }
        }

        public int ActivationBehavior
        {
            get
            {
                return (int)_colorPickerSettings.Properties.ActivationAction;
            }

            set
            {
                if (value != (int)_colorPickerSettings.Properties.ActivationAction)
                {
                    _colorPickerSettings.Properties.ActivationAction = (ColorPickerActivationAction)value;
                    OnPropertyChanged(nameof(ActivationBehavior));
                    NotifySettingsChanged();
                }
            }
        }

        public bool ShowColorName
        {
            get => _colorPickerSettings.Properties.ShowColorName;
            set
            {
                if (_colorPickerSettings.Properties.ShowColorName != value)
                {
                    _colorPickerSettings.Properties.ShowColorName = value;
                    OnPropertyChanged(nameof(ShowColorName));
                    NotifySettingsChanged();
                }
            }
        }

        public ObservableCollection<ColorFormatModel> ColorFormats { get; } = new ObservableCollection<ColorFormatModel>();

        public Dictionary<string, string> ColorFormatsPreview
        {
            get => _colorFormatsPreview;
            set
            {
                _colorFormatsPreview = value;
                OnPropertyChanged(nameof(ColorFormatsPreview));
            }
        }

        public int ColorFormatsPreviewIndex
        {
            get
            {
                return _colorFormatPreviewIndex;
            }

            set
            {
                if (value != _colorFormatPreviewIndex)
                {
                    _colorFormatPreviewIndex = value;
                    OnPropertyChanged(nameof(ColorFormatsPreviewIndex));
                }
            }
        }

        private void InitializeColorFormats()
        {
            foreach (var storedColorFormat in _colorPickerSettings.Properties.VisibleColorFormats)
            {
                // skip entries with empty name or duplicated name, it should never occur
                string storedName = storedColorFormat.Key;
                if (storedName == string.Empty || ColorFormats.Any(x => x.Name.ToUpperInvariant().Equals(storedName.ToUpperInvariant(), StringComparison.Ordinal)))
                {
                    continue;
                }

                string format = storedColorFormat.Value.Value;
                if (format == string.Empty)
                {
                    format = ColorFormatHelper.GetDefaultFormat(storedName);
                }

                ColorFormatModel customColorFormat = new ColorFormatModel(storedName, format, storedColorFormat.Value.Key);
                customColorFormat.PropertyChanged += ColorFormat_PropertyChanged;
                ColorFormats.Add(customColorFormat);
            }

            // Reordering colors with buttons: disable first and last buttons
            ColorFormats[0].CanMoveUp = false;
            ColorFormats[ColorFormats.Count - 1].CanMoveDown = false;

            UpdateColorFormatPreview();
            ColorFormats.CollectionChanged += ColorFormats_CollectionChanged;
        }

        private void UpdateColorFormatPreview()
        {
            ColorFormatsPreview = ColorFormats.Select(x => new KeyValuePair<string, string>(x.Name, x.Name + " - " + x.Example)).ToDictionary(x => x.Key, x => x.Value);
            SetPreviewSelectedIndex();
            ScheduleSavingOfSettings();
        }

        private void ColorFormats_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Reordering colors with buttons: update buttons availability depending on order
            if (ColorFormats.Count > 0)
            {
                foreach (var color in ColorFormats)
                {
                    color.CanMoveUp = true;
                    color.CanMoveDown = true;
                }

                ColorFormats[0].CanMoveUp = false;
                ColorFormats[ColorFormats.Count - 1].CanMoveDown = false;
            }

            if (ColorFormats.Count == 1)
            {
                ColorFormats.Single().CanBeDeleted = false;
            }
            else
            {
                foreach (var color in ColorFormats)
                {
                    color.CanBeDeleted = true;
                }
            }

            UpdateColorFormats();
            UpdateColorFormatPreview();
        }

        private void ColorFormat_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Remaining properties are handled by the collection and by the dialog
            if (e.PropertyName == nameof(ColorFormatModel.IsShown))
            {
                UpdateColorFormats();
                ScheduleSavingOfSettings();
            }
        }

        private void ScheduleSavingOfSettings()
        {
            lock (_delayedActionLock)
            {
                if (_delayedTimer.Enabled)
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
                _colorPickerSettings.Properties.VisibleColorFormats.Add(colorFormat.Name, new KeyValuePair<bool, string>(colorFormat.IsShown, colorFormat.Format));
            }
        }

        internal void AddNewColorFormat(string newColorName, string newColorFormat, bool isShown)
        {
            if (ColorFormats.Count > 0)
            {
                ColorFormats[0].CanMoveUp = true;
            }

            ColorFormatModel newModel = new ColorFormatModel(newColorName, newColorFormat, isShown);
            newModel.PropertyChanged += ColorFormat_PropertyChanged;
            ColorFormats.Insert(0, newModel);
            SetPreviewSelectedIndex();
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       ColorPickerSettings.ModuleName,
                       JsonSerializer.Serialize(_colorPickerSettings, SourceGenerationContextContext.Default.ColorPickerSettings)));
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _delayedTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        internal ColorFormatModel GetNewColorFormatModel()
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            string defaultName = resourceLoader.GetString("CustomColorFormatDefaultName");
            ColorFormatModel newColorFormatModel = new ColorFormatModel();
            newColorFormatModel.Name = defaultName;
            int extensionNumber = 1;
            while (ColorFormats.Any(x => x.Name.Equals(newColorFormatModel.Name, StringComparison.Ordinal)))
            {
                newColorFormatModel.Name = defaultName + " (" + extensionNumber + ")";
                extensionNumber++;
            }

            return newColorFormatModel;
        }

        internal bool SetValidity(ColorFormatModel colorFormatModel, string oldName)
        {
            if ((colorFormatModel.Format == string.Empty) || (colorFormatModel.Name == string.Empty))
            {
                colorFormatModel.IsValid = false;
            }
            else if (colorFormatModel.Name == oldName)
            {
                colorFormatModel.IsValid = true;
            }
            else
            {
                colorFormatModel.IsValid = ColorFormats.Count(x => x.Name.ToUpperInvariant().Equals(colorFormatModel.Name.ToUpperInvariant(), StringComparison.Ordinal))
                    < (colorFormatModel.IsNew ? 1 : 2);
            }

            return colorFormatModel.IsValid;
        }

        internal int DeleteModel(ColorFormatModel colorFormatModel)
        {
            var deleteIndex = ColorFormats.IndexOf(colorFormatModel);
            ColorFormats.Remove(colorFormatModel);
            return deleteIndex;
        }

        internal void UpdateColorFormat(string oldName, ColorFormatModel colorFormat)
        {
            if (SelectedColorRepresentationValue == oldName)
            {
                SelectedColorRepresentationValue = colorFormat.Name;    // name might be changed by the user
            }

            UpdateColorFormats();
            UpdateColorFormatPreview();
        }

        internal void SetPreviewSelectedIndex()
        {
            int index = 0;

            foreach (var item in ColorFormats)
            {
                if (item.Name == SelectedColorRepresentationValue)
                {
                    break;
                }

                index++;
            }

            if (index >= ColorFormats.Count)
            {
                index = 0;
            }

            ColorFormatsPreviewIndex = index;
        }
    }
}
