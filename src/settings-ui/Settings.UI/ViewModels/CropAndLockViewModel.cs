// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class CropAndLockViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private CropAndLockSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public CropAndLockViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<CropAndLockSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of CropAndLock.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            _reparentHotkey = Settings.Properties.ReparentHotkey.Value;
            _thumbnailHotkey = Settings.Properties.ThumbnailHotkey.Value;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredCropAndLockEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.CropAndLock;
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
                    GeneralSettingsConfig.Enabled.CropAndLock = value;
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

        public HotkeySettings ReparentActivationShortcut
        {
            get => _reparentHotkey;

            set
            {
                if (value != _reparentHotkey)
                {
                    if (value == null)
                    {
                        _reparentHotkey = CropAndLockProperties.DefaultReparentHotkeyValue;
                    }
                    else
                    {
                        _reparentHotkey = value;
                    }

                    Settings.Properties.ReparentHotkey.Value = _reparentHotkey;
                    NotifyPropertyChanged();

                    // Using InvariantCulture as this is an IPC message
                    SendConfigMSG(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                            CropAndLockSettings.ModuleName,
                            JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.CropAndLockSettings)));
                }
            }
        }

        public HotkeySettings ThumbnailActivationShortcut
        {
            get => _thumbnailHotkey;

            set
            {
                if (value != _thumbnailHotkey)
                {
                    if (value == null)
                    {
                        _thumbnailHotkey = CropAndLockProperties.DefaultThumbnailHotkeyValue;
                    }
                    else
                    {
                        _thumbnailHotkey = value;
                    }

                    Settings.Properties.ThumbnailHotkey.Value = _thumbnailHotkey;
                    NotifyPropertyChanged();

                    // Using InvariantCulture as this is an IPC message
                    SendConfigMSG(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                            CropAndLockSettings.ModuleName,
                            JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.CropAndLockSettings)));
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), CropAndLockSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private HotkeySettings _reparentHotkey;
        private HotkeySettings _thumbnailHotkey;
    }
}
