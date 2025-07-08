// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AlwaysOnTopViewModel : PageViewModelBase
    {
        protected override string ModuleName => AlwaysOnTopSettings.ModuleName;

        private bool _hotkeyHasConflict;
        private string _hotkeyTooltip;

        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private AlwaysOnTopSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public AlwaysOnTopViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<AlwaysOnTopSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
            : base(ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of AlwaysOnTop.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            _hotkey = Settings.Properties.Hotkey.Value;
            _frameEnabled = Settings.Properties.FrameEnabled.Value;
            _frameThickness = Settings.Properties.FrameThickness.Value;
            _frameColor = Settings.Properties.FrameColor.Value;
            _frameOpacity = Settings.Properties.FrameOpacity.Value;
            _frameAccentColor = Settings.Properties.FrameAccentColor.Value;
            _soundEnabled = Settings.Properties.SoundEnabled.Value;
            _doNotActivateOnGameMode = Settings.Properties.DoNotActivateOnGameMode.Value;
            _roundCornersEnabled = Settings.Properties.RoundCornersEnabled.Value;
            _excludedApps = Settings.Properties.ExcludedApps.Value;
            _windows11 = OSVersionHelper.IsWindows11();

            CheckAndUpdateHotkeyName();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            RegisterHotkeySettings(Hotkey);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.AlwaysOnTop;
            }
        }

        protected override void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            UpdateHotkeyConflictStatus(e.Conflicts);

            // Update properties using setters to trigger PropertyChanged
            void UpdateConflictProperties()
            {
                HotkeyHasConflict = GetHotkeyConflictStatus(AlwaysOnTopProperties.DefaultHotkeyValue.HotkeyName);
                HotkeyTooltip = GetHotkeyConflictTooltip(AlwaysOnTopProperties.DefaultHotkeyValue.HotkeyName);
            }

            _ = Task.Run(() =>
            {
                try
                {
                    var settingsWindow = App.GetSettingsWindow();
                    if (settingsWindow?.DispatcherQueue != null)
                    {
                        settingsWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, UpdateConflictProperties);
                    }
                    else
                    {
                        UpdateConflictProperties();
                    }
                }
                catch
                {
                    UpdateConflictProperties();
                }
            });
        }

        public bool HotkeyHasConflict
        {
            get => _hotkeyHasConflict;
            set
            {
                if (_hotkeyHasConflict != value)
                {
                    _hotkeyHasConflict = value;
                    OnPropertyChanged(nameof(HotkeyHasConflict));
                }
            }
        }

        public string HotkeyTooltip
        {
            get => _hotkeyTooltip;
            set
            {
                if (_hotkeyTooltip != value)
                {
                    _hotkeyTooltip = value;
                    OnPropertyChanged(nameof(HotkeyTooltip));
                }
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
                    GeneralSettingsConfig.Enabled.AlwaysOnTop = value;
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

        public HotkeySettings Hotkey
        {
            get => _hotkey;

            set
            {
                if (value != _hotkey)
                {
                    if (value == null || value.IsEmpty())
                    {
                        _hotkey = AlwaysOnTopProperties.DefaultHotkeyValue;
                    }
                    else
                    {
                        _hotkey = value;
                    }

                    Settings.Properties.Hotkey.Value = _hotkey;
                    NotifyPropertyChanged();

                    // Using InvariantCulture as this is an IPC message
                    SendConfigMSG(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                            AlwaysOnTopSettings.ModuleName,
                            JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.AlwaysOnTopSettings)));
                }
            }
        }

        public bool FrameEnabled
        {
            get => _frameEnabled;

            set
            {
                if (value != _frameEnabled)
                {
                    _frameEnabled = value;
                    Settings.Properties.FrameEnabled.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int FrameThickness
        {
            get => _frameThickness;

            set
            {
                if (value != _frameThickness)
                {
                    _frameThickness = value;
                    Settings.Properties.FrameThickness.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string FrameColor
        {
            get => _frameColor;

            set
            {
                if (value != _frameColor)
                {
                    _frameColor = value;
                    Settings.Properties.FrameColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int FrameOpacity
        {
            get => _frameOpacity;

            set
            {
                if (value != _frameOpacity)
                {
                    _frameOpacity = value;
                    Settings.Properties.FrameOpacity.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool SoundEnabled
        {
            get => _soundEnabled;

            set
            {
                if (value != _soundEnabled)
                {
                    _soundEnabled = value;
                    Settings.Properties.SoundEnabled.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool DoNotActivateOnGameMode
        {
            get => _doNotActivateOnGameMode;

            set
            {
                if (value != _doNotActivateOnGameMode)
                {
                    _doNotActivateOnGameMode = value;
                    Settings.Properties.DoNotActivateOnGameMode.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool RoundCornersEnabled
        {
            get => _roundCornersEnabled;

            set
            {
                if (value != _roundCornersEnabled)
                {
                    _roundCornersEnabled = value;
                    Settings.Properties.RoundCornersEnabled.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string ExcludedApps
        {
            get => _excludedApps;

            set
            {
                if (value != _excludedApps)
                {
                    _excludedApps = value;
                    Settings.Properties.ExcludedApps.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool FrameAccentColor
        {
            get => _frameAccentColor;

            set
            {
                if (value != _frameAccentColor)
                {
                    _frameAccentColor = value;
                    Settings.Properties.FrameAccentColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool Windows11
        {
            get => _windows11;

            set
            {
                _windows11 = value;
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), AlwaysOnTopSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void CheckAndUpdateHotkeyName()
        {
            if (Settings.Properties.Hotkey.Value.HotkeyName == string.Empty)
            {
                Settings.Properties.Hotkey.Value.HotkeyName = AlwaysOnTopProperties.DefaultHotkeyValue.HotkeyName;
                Settings.Properties.Hotkey.Value.OwnerModuleName = AlwaysOnTopSettings.ModuleName;
                SettingsUtils.SaveSettings(Settings.ToJsonString(), AlwaysOnTopSettings.ModuleName);
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private HotkeySettings _hotkey;
        private bool _frameEnabled;
        private int _frameThickness;
        private string _frameColor;
        private bool _frameAccentColor;
        private int _frameOpacity;
        private bool _soundEnabled;
        private bool _doNotActivateOnGameMode;
        private bool _roundCornersEnabled;
        private string _excludedApps;
        private bool _windows11;
    }
}
