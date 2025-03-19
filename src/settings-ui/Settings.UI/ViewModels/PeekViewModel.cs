// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Dispatching;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PeekViewModel : Observable, IDisposable
    {
        private bool _isEnabled;

        private bool _settingsUpdating;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly ISettingsUtils _settingsUtils;
        private readonly PeekPreviewSettings _peekPreviewSettings;
        private PeekSettings _peekSettings;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;

        private Func<string, int> SendConfigMSG { get; }

        private IFileSystemWatcher _watcher;

        public PeekViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc,
            DispatcherQueue dispatcherQueue)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // Load the application-specific settings, including preview items.
            _peekSettings = _settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekSettings.ModuleName);
            _peekPreviewSettings = _settingsUtils.GetSettingsOrDefault<PeekPreviewSettings>(PeekSettings.ModuleName, PeekPreviewSettings.FileName);
            SetupSettingsFileWatcher();

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc;
        }

        /// <summary>
        /// Set up the file watcher for the settings file. Used to respond to updates to the
        /// ConfirmFileDelete setting by the user within the Peek application itself.
        /// </summary>
        private void SetupSettingsFileWatcher()
        {
            string settingsPath = _settingsUtils.GetSettingsFilePath(PeekSettings.ModuleName);

            _watcher = Helper.GetFileWatcher(PeekSettings.ModuleName, SettingsUtils.DefaultFileName, () =>
            {
                try
                {
                    _settingsUpdating = true;
                    var newSettings = _settingsUtils.GetSettings<PeekSettings>(PeekSettings.ModuleName);

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            ConfirmFileDelete = newSettings.Properties.ConfirmFileDelete.Value;
                            _peekSettings = newSettings;
                        }
                        finally
                        {
                            // Only clear the flag once the UI update is complete.
                            _settingsUpdating = false;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load Peek settings: {ex.Message}", ex);
                    _settingsUpdating = false;
                }
            });
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

        public bool ConfirmFileDelete
        {
            get => _peekSettings.Properties.ConfirmFileDelete.Value;
            set
            {
                if (_peekSettings.Properties.ConfirmFileDelete.Value != value)
                {
                    _peekSettings.Properties.ConfirmFileDelete.Value = value;
                    OnPropertyChanged(nameof(ConfirmFileDelete));
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
            // Do not send IPC message if the settings file has been updated by Peek itself.
            if (_settingsUpdating)
            {
                return;
            }

            // This message will be intercepted by the runner, which passes the serialized JSON to
            // Peek.set_config() in the C++ Peek project, which then saves it to file.
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

        public void Dispose()
        {
            _watcher?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
