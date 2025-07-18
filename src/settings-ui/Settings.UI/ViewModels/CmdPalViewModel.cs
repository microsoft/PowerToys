// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Windows.Management.Deployment;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class CmdPalViewModel : PageViewModelBase
    {
        protected override string ModuleName => "CmdPal";

        private bool _hotkeyHasConflict;
        private string _hotkeyTooltip;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _isEnabled;
        private HotkeySettings _hotkey;
        private IFileSystemWatcher _watcher;
        private DispatcherQueue _uiDispatcherQueue;
        private CmdPalProperties _cmdPalProperties;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public CmdPalViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, DispatcherQueue uiDispatcherQueue)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _uiDispatcherQueue = uiDispatcherQueue;
            _cmdPalProperties = new CmdPalProperties();

            InitializeEnabledValue();

            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

#if DEBUG
            var settingsPath = Path.Combine(localAppDataDir, "Packages", "Microsoft.CommandPalette.Dev_8wekyb3d8bbwe", "LocalState", "settings.json");
#else
            var settingsPath = Path.Combine(localAppDataDir, "Packages", "Microsoft.CommandPalette_8wekyb3d8bbwe", "LocalState", "settings.json");
#endif

            _hotkey = _cmdPalProperties.Hotkey;

            _watcher = Helper.GetFileWatcher(settingsPath, () =>
            {
                _cmdPalProperties.InitializeHotkey();
                _hotkey = _cmdPalProperties.Hotkey;

                _uiDispatcherQueue.TryEnqueue(() =>
                {
                    OnPropertyChanged(nameof(Hotkey));
                });
            });

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredCmdPalEnabledValue();
            if (_enabledGpoRuleConfiguration is GpoRuleConfigured.Disabled or GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                IsEnabledGpoConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.CmdPal;
            }
        }

        protected override void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            UpdateHotkeyConflictStatus(e.Conflicts);

            // Update properties using setters to trigger PropertyChanged
            void UpdateConflictProperties()
            {
                HotkeyHasConflict = GetHotkeyConflictStatus(CmdPalProperties.DefaultHotkeyValue.HotkeyName);
                HotkeyTooltip = GetHotkeyConflictTooltip(CmdPalProperties.DefaultHotkeyValue.HotkeyName);
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
                if (IsEnabledGpoConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.CmdPal = value;
                    OutGoingGeneralSettings snd = new(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public HotkeySettings Hotkey
        {
            get => _hotkey;

            private set
            {
            }
        }

        public bool IsEnabledGpoConfigured { get; private set; }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
