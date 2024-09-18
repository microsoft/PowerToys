// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AllExperiments;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ZoomItViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ZoomItSettings _zoomItSettings;

        private Func<string, int> SendConfigMSG { get; }

        private Func<string, string, string> PickFileDialog { get; }

        public ButtonClickCommand SelectDemoTypeFileCommand { get; set; }

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            MaxDepth = 0,
            IncludeFields = true,
        };

        public ZoomItViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<string, string, string> pickFileDialog)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            var zoomItSettings = global::PowerToys.ZoomItSettingsInterop.ZoomItSettings.LoadSettingsJson();
            _zoomItSettings = JsonSerializer.Deserialize<ZoomItSettings>(zoomItSettings, _serializerOptions);

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message for the enabled value.
            SendConfigMSG = ipcMSGCallBackFunc;

            // set the callback for when we need the user to pick a file.
            PickFileDialog = pickFileDialog;

            SelectDemoTypeFileCommand = new ButtonClickCommand(SelectDemoTypeFileAction);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredZoomItEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ZoomIt;
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

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.ZoomIt = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool ShowTrayIcon
        {
            get => _zoomItSettings.Properties.ShowTrayIcon.Value;
            set
            {
                if (_zoomItSettings.Properties.ShowTrayIcon.Value != value)
                {
                    _zoomItSettings.Properties.ShowTrayIcon.Value = value;
                    OnPropertyChanged(nameof(ShowTrayIcon));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings ZoomToggleKey
        {
            get => _zoomItSettings.Properties.ToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.ToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.ToggleKey.Value = value ?? ZoomItProperties.DefaultToggleKey;
                    OnPropertyChanged(nameof(ZoomToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public bool AnimateZoom
        {
            get => _zoomItSettings.Properties.AnimnateZoom.Value;
            set
            {
                if (_zoomItSettings.Properties.AnimnateZoom.Value != value)
                {
                    _zoomItSettings.Properties.AnimnateZoom.Value = value;
                    OnPropertyChanged(nameof(AnimateZoom));
                    NotifySettingsChanged();
                }
            }
        }

        public int ZoominSliderLevel
        {
            get => _zoomItSettings.Properties.ZoominSliderLevel.Value;
            set
            {
                if (_zoomItSettings.Properties.ZoominSliderLevel.Value != value)
                {
                    _zoomItSettings.Properties.ZoominSliderLevel.Value = value;
                    OnPropertyChanged(nameof(ZoominSliderLevel));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings LiveZoomToggleKey
        {
            get => _zoomItSettings.Properties.LiveZoomToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.LiveZoomToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.LiveZoomToggleKey.Value = value ?? ZoomItProperties.DefaultLiveZoomToggleKey;
                    OnPropertyChanged(nameof(LiveZoomToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings DrawToggleKey
        {
            get => _zoomItSettings.Properties.DrawToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.DrawToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.DrawToggleKey.Value = value ?? ZoomItProperties.DefaultDrawToggleKey;
                    OnPropertyChanged(nameof(DrawToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings RecordToggleKey
        {
            get => _zoomItSettings.Properties.RecordToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.RecordToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.RecordToggleKey.Value = value ?? ZoomItProperties.DefaultRecordToggleKey;
                    OnPropertyChanged(nameof(RecordToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings SnipToggleKey
        {
            get => _zoomItSettings.Properties.SnipToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.SnipToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.SnipToggleKey.Value = value ?? ZoomItProperties.DefaultSnipToggleKey;
                    OnPropertyChanged(nameof(SnipToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings BreakTimerKey
        {
            get => _zoomItSettings.Properties.BreakTimerKey.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakTimerKey.Value != value)
                {
                    _zoomItSettings.Properties.BreakTimerKey.Value = value ?? ZoomItProperties.DefaultBreakTimerKey;
                    OnPropertyChanged(nameof(BreakTimerKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings DemoTypeToggleKey
        {
            get => _zoomItSettings.Properties.DemoTypeToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeToggleKey.Value = value ?? ZoomItProperties.DefaultDemoTypeToggleKey;
                    OnPropertyChanged(nameof(DemoTypeToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public string DemoTypeFile
        {
            get => _zoomItSettings.Properties.DemoTypeFile.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeFile.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeFile.Value = value;
                    OnPropertyChanged(nameof(DemoTypeFile));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            global::PowerToys.ZoomItSettingsInterop.ZoomItSettings.SaveSettingsJson(
                JsonSerializer.Serialize(_zoomItSettings));
        }

        private void SelectDemoTypeFileAction()
        {
            // TODO: Localize
            try
            {
                ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
                string title = resourceLoader.GetString("ZoomIt_DemoType_File_Picker_Dialog_Title");
                string allFilesFilter = resourceLoader.GetString("FilePicker_AllFilesFilter");
                string pickedFile = PickFileDialog($"{allFilesFilter}\0*.*\0\0", title);
                if (pickedFile != null)
                {
                    DemoTypeFile = pickedFile;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking Demo Type file.", ex);
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
    }
}
