// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PeekViewModel : Observable
    {
        private bool _isEnabled;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly PeekSettings _peekSettings;
        private readonly PeekPreviewSettings _peekPreviewSettings;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;

        private Func<string, int> SendConfigMSG { get; }

        public PeekViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            if (_settingsUtils.SettingsExists(PeekSettings.ModuleName))
            {
                _peekSettings = _settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekSettings.ModuleName);
            }
            else
            {
                _peekSettings = new PeekSettings();
            }

            if (_settingsUtils.SettingsExists(PeekSettings.ModuleName, PeekPreviewSettings.FileName))
            {
                _peekPreviewSettings = _settingsUtils.GetSettingsOrDefault<PeekPreviewSettings>(PeekSettings.ModuleName, PeekPreviewSettings.FileName);
            }
            else
            {
                _peekPreviewSettings = new PeekPreviewSettings();
            }

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPeekEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.Peek;
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

                    GeneralSettingsConfig.Enabled.Peek = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public HotkeySettings ActivationShortcut
        {
            get => _peekSettings.Properties.ActivationShortcut;
            set
            {
                if (_peekSettings.Properties.ActivationShortcut != value)
                {
                    _peekSettings.Properties.ActivationShortcut = value ?? _peekSettings.Properties.DefaultActivationShortcut;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public bool AlwaysRunNotElevated
        {
            get => _peekSettings.Properties.AlwaysRunNotElevated.Value;
            set
            {
                if (_peekSettings.Properties.AlwaysRunNotElevated.Value != value)
                {
                    _peekSettings.Properties.AlwaysRunNotElevated.Value = value;
                    OnPropertyChanged(nameof(AlwaysRunNotElevated));
                    NotifySettingsChanged();
                }
            }
        }

        public bool CloseAfterLosingFocus
        {
            get => _peekSettings.Properties.CloseAfterLosingFocus.Value;
            set
            {
                if (_peekSettings.Properties.CloseAfterLosingFocus.Value != value)
                {
                    _peekSettings.Properties.CloseAfterLosingFocus.Value = value;
                    OnPropertyChanged(nameof(CloseAfterLosingFocus));
                    NotifySettingsChanged();
                }
            }
        }

        public bool SourceCodeWrapText
        {
            get => _peekPreviewSettings.SourceCodeWrapText.Value;
            set
            {
                if (_peekPreviewSettings.SourceCodeWrapText.Value != value)
                {
                    _peekPreviewSettings.SourceCodeWrapText.Value = value;
                    OnPropertyChanged(nameof(SourceCodeWrapText));
                    SavePreviewSettings();
                }
            }
        }

        public bool SourceCodeTryFormat
        {
            get => _peekPreviewSettings.SourceCodeTryFormat.Value;
            set
            {
                if (_peekPreviewSettings.SourceCodeTryFormat.Value != value)
                {
                    _peekPreviewSettings.SourceCodeTryFormat.Value = value;
                    OnPropertyChanged(nameof(SourceCodeTryFormat));
                    SavePreviewSettings();
                }
            }
        }

        public int SourceCodeFontSize
        {
            get => _peekPreviewSettings.SourceCodeFontSize.Value;
            set
            {
                if (_peekPreviewSettings.SourceCodeFontSize.Value != value)
                {
                    _peekPreviewSettings.SourceCodeFontSize.Value = value;
                    OnPropertyChanged(nameof(SourceCodeFontSize));
                    SavePreviewSettings();
                }
            }
        }

        public bool SourceCodeStickyScroll
        {
            get => _peekPreviewSettings.SourceCodeStickyScroll.Value;
            set
            {
                if (_peekPreviewSettings.SourceCodeStickyScroll.Value != value)
                {
                    _peekPreviewSettings.SourceCodeStickyScroll.Value = value;
                    OnPropertyChanged(nameof(SourceCodeStickyScroll));
                    SavePreviewSettings();
                }
            }
        }

        public bool SourceCodeMinimap
        {
            get => _peekPreviewSettings.SourceCodeMinimap.Value;
            set
            {
                if (_peekPreviewSettings.SourceCodeMinimap.Value != value)
                {
                    _peekPreviewSettings.SourceCodeMinimap.Value = value;
                    OnPropertyChanged(nameof(SourceCodeMinimap));
                    SavePreviewSettings();
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
                    JsonSerializer.Serialize(_peekSettings, SourceGenerationContextContext.Default.PeekSettings)));
        }

        private void SavePreviewSettings()
        {
            _settingsUtils.SaveSettings(_peekPreviewSettings.ToJsonString(), PeekSettings.ModuleName, PeekPreviewSettings.FileName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
