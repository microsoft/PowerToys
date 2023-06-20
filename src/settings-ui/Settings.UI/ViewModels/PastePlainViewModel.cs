// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Timers;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PastePlainViewModel : Observable, IDisposable
    {
        private bool disposedValue;

        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it, otherwise we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly object _delayedActionLock = new object();

        private readonly PastePlainSettings _pastePlainSettings;
        private Timer _delayedTimer;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public PastePlainViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<PastePlainSettings> pastePlainSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // To obtain the settings configurations of Fancy zones.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            if (pastePlainSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(pastePlainSettingsRepository));
            }

            _pastePlainSettings = pastePlainSettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPastePlainEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.PastePlain;
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

                    // Set the status of PastePlain in the general settings
                    GeneralSettingsConfig.Enabled.PastePlain = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

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
            get => _pastePlainSettings.Properties.ActivationShortcut;
            set
            {
                if (_pastePlainSettings.Properties.ActivationShortcut != value)
                {
                    _pastePlainSettings.Properties.ActivationShortcut = value ?? _pastePlainSettings.Properties.DefaultActivationShortcut;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    OnPropertyChanged(nameof(IsConflictingCopyShortcut));

                    _settingsUtils.SaveSettings(_pastePlainSettings.ToJsonString(), PastePlainSettings.ModuleName);
                    NotifySettingsChanged();
                }
            }
        }

        public bool IsConflictingCopyShortcut
        {
            get
            {
                return ActivationShortcut.ToString() == "Ctrl + V" || ActivationShortcut.ToString() == "Ctrl + Shift + V";
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

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       PastePlainSettings.ModuleName,
                       JsonSerializer.Serialize(_pastePlainSettings)));
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
    }
}
