// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AwakePage : Page, IRefreshablePage
    {
        private readonly string _appName = "Awake";
        private readonly SettingsUtils _settingsUtils;

        private readonly SettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly SettingsRepository<AwakeSettings> _moduleSettingsRepository;

        private readonly IFileSystem _fileSystem;
        private readonly IFileSystemWatcher _fileSystemWatcher;

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly Func<string, int> _sendConfigMsg;

        private AwakeViewModel ViewModel { get; set; }

        public AwakePage()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _fileSystem = new FileSystem();
            _settingsUtils = new SettingsUtils();
            _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            ViewModel = new AwakeViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            _moduleSettingsRepository = SettingsRepository<AwakeSettings>.GetInstance(_settingsUtils);

            // We load the view model settings first.
            LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);

            DataContext = ViewModel;

            var settingsPath = _settingsUtils.GetSettingsFilePath(_appName);

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.CreateNew();
            _fileSystemWatcher.Path = _fileSystem.Path.GetDirectoryName(settingsPath);
            _fileSystemWatcher.Filter = _fileSystem.Path.GetFileName(settingsPath);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileSystemWatcher.Changed += Settings_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;

            InitializeComponent();
        }

        /// <summary>
        /// Triggered whenever a view model property changes. This is done in addition to the baked-in view model changes.
        /// </summary>
        /// <remarks>
        /// TODO: The logic here needs to be optimized since doing string comparison on values is not ideal.
        /// </remarks>
        /// <param name="sender">Sender of the change.</param>
        /// <param name="e">Property parameter.</param>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_sendConfigMsg != null)
            {
                if (e.PropertyName == "IsEnabled")
                {
                    if (ViewModel.IsEnabled != _generalSettingsRepository.SettingsConfig.Enabled.Awake)
                    {
                        _generalSettingsRepository.SettingsConfig.Enabled.Awake = ViewModel.IsEnabled;

                        var generalSettingsMessage = new OutGoingGeneralSettings(_generalSettingsRepository.SettingsConfig).ToString();

                        Logger.LogInfo($"Saved general settings from Awake page.");
                        _sendConfigMsg(generalSettingsMessage);
                    }
                }
                else
                {
                    if (ViewModel.ModuleSettings != null)
                    {
                        SndAwakeSettings currentSettings = new(_moduleSettingsRepository.SettingsConfig);
                        SndModuleSettings<SndAwakeSettings> csIpcMessage = new(currentSettings);

                        SndAwakeSettings outSettings = new(ViewModel.ModuleSettings);
                        SndModuleSettings<SndAwakeSettings> outIpcMessage = new(outSettings);

                        string csMessage = csIpcMessage.ToJsonString();
                        string outMessage = outIpcMessage.ToJsonString();

                        if (!csMessage.Equals(outMessage, StringComparison.Ordinal))
                        {
                            Logger.LogInfo($"Saved Awake settings from Awake page.");
                            _sendConfigMsg(outMessage);
                        }
                    }
                }
            }
        }

        private void LoadSettings(ISettingsRepository<GeneralSettings> generalSettingsRepository, ISettingsRepository<AwakeSettings> moduleSettingsRepository)
        {
            if (generalSettingsRepository != null)
            {
                if (moduleSettingsRepository != null)
                {
                    UpdateViewModelSettings(moduleSettingsRepository.SettingsConfig, generalSettingsRepository.SettingsConfig);
                }
                else
                {
                    throw new ArgumentNullException(nameof(moduleSettingsRepository));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(generalSettingsRepository));
            }
        }

        private void UpdateViewModelSettings(AwakeSettings awakeSettings, GeneralSettings generalSettings)
        {
            if (awakeSettings != null)
            {
                if (generalSettings != null)
                {
                    ViewModel.IsEnabled = generalSettings.Enabled.Awake;
                    ViewModel.ModuleSettings = (AwakeSettings)awakeSettings.Clone();

                    UpdateEnabledState(generalSettings.Enabled.Awake);
                }
                else
                {
                    throw new ArgumentNullException(nameof(generalSettings));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(awakeSettings));
            }
        }

        /// <summary>
        /// Updates the tool enablement state.
        /// </summary>
        /// <param name="recommendedState">The state that is recommended for the tool, but can be overridden if a GPO policy is in place.</param>
        private void UpdateEnabledState(bool recommendedState)
        {
            var enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredAwakeEnabledValue();

            if (enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                ViewModel.IsEnabledGpoConfigured = true;
                ViewModel.IsEnabled = enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                ViewModel.IsEnabled = recommendedState;
            }
        }

        private void Settings_Changed(object sender, FileSystemEventArgs e)
        {
            bool taskAdded = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                _moduleSettingsRepository.ReloadSettings();
                LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);
            });
        }

        public void RefreshEnabledState()
        {
            UpdateEnabledState(_generalSettingsRepository.SettingsConfig.Enabled.Awake);
            ViewModel.RefreshEnabledState();
        }
    }
}
