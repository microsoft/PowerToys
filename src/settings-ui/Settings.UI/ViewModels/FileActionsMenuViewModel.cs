// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class FileActionsMenuViewModel : Observable
    {
        private const string ModuleName = FileActionsMenuSettings.ModuleName;

        private FileActionsMenuSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _fileActionsMenuEnabledGpoRuleConfiguration;
        private bool _fileActionsMenuEnabledStateIsGPOConfigured;
        private bool _fileActionsMenuIsEnabled;
        private HotkeySettings _fileActionsMenuShortcut;

        public FileActionsMenuViewModel(ISettingsRepository<FileActionsMenuSettings> moduleSettingsRepository, ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // To obtain the general Settings configurations of PowerToys
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            // To obtain the PowerPreview settings if it exists.
            // If the file does not exist, to create a new one and return the default settings configurations.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _fileActionsMenuEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredSvgPreviewEnabledValue();
            if (_fileActionsMenuEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _fileActionsMenuEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _fileActionsMenuEnabledStateIsGPOConfigured = true;
                _fileActionsMenuIsEnabled = _fileActionsMenuEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _fileActionsMenuIsEnabled = Settings.Properties.EnableFileActionsMenu;
            }

            _fileActionsMenuShortcut = Settings.Properties.FileActionsMenuShortcut;
        }

        public bool FileActionsMenuIsEnabled
        {
            get
            {
                return _fileActionsMenuIsEnabled;
            }

            set
            {
                if (_fileActionsMenuEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _fileActionsMenuIsEnabled)
                {
                    _fileActionsMenuIsEnabled = value;
                    Settings.Properties.EnableFileActionsMenu = value;
                    RaisePropertyChanged();
                }
            }
        }

        public HotkeySettings FileActionsMenuShortcut
        {
            get => _fileActionsMenuShortcut;
            set
            {
                if (value != _fileActionsMenuShortcut)
                {
                    _fileActionsMenuShortcut = value ?? Settings.Properties.DefaultFileActionsMenuShortcut;
                    Settings.Properties.FileActionsMenuShortcut = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsFileActionsMenuEnabledGpoConfigured
        {
            get => _fileActionsMenuEnabledStateIsGPOConfigured;
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (SendConfigMSG != null)
            {
                SndFileActionsMenuSettings snd = new SndFileActionsMenuSettings(Settings);
                SndModuleSettings<SndFileActionsMenuSettings> ipcMessage = new(snd);
                SendConfigMSG(ipcMessage.ToJsonString());
            }
        }
    }
}
