// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class MouseUtilsViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private FindMyMouseSettings FindMyMouseSettingsConfig { get; set; }

        private MouseHighlighterSettings MouseHighlighterSettingsConfig { get; set; }

        public MouseUtilsViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<FindMyMouseSettings> findMyMouseSettingsRepository, ISettingsRepository<MouseHighlighterSettings> mouseHighlighterSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _isFindMyMouseEnabled = GeneralSettingsConfig.Enabled.FindMyMouse;

            _isMouseHighlighterEnabled = GeneralSettingsConfig.Enabled.MouseHighlighter;

            // To obtain the find my mouse settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            if (findMyMouseSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(findMyMouseSettingsRepository));
            }

            FindMyMouseSettingsConfig = findMyMouseSettingsRepository.SettingsConfig;
            _findMyMouseDoNotActivateOnGameMode = FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value;

            if (mouseHighlighterSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(mouseHighlighterSettingsRepository));
            }

            MouseHighlighterSettingsConfig = mouseHighlighterSettingsRepository.SettingsConfig;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        public bool IsFindMyMouseEnabled
        {
            get => _isFindMyMouseEnabled;
            set
            {
                if (_isFindMyMouseEnabled != value)
                {
                    _isFindMyMouseEnabled = value;

                    GeneralSettingsConfig.Enabled.FindMyMouse = value;
                    OnPropertyChanged(nameof(IsFindMyMouseEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public bool FindMyMouseDoNotActivateOnGameMode
        {
            get
            {
                return _findMyMouseDoNotActivateOnGameMode;
            }

            set
            {
                if (_findMyMouseDoNotActivateOnGameMode != value)
                {
                    _findMyMouseDoNotActivateOnGameMode = value;
                    FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public void NotifyFindMyMousePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndFindMyMouseSettings outsettings = new SndFindMyMouseSettings(FindMyMouseSettingsConfig);
            SndModuleSettings<SndFindMyMouseSettings> ipcMessage = new SndModuleSettings<SndFindMyMouseSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(FindMyMouseSettingsConfig.ToJsonString(), FindMyMouseSettings.ModuleName);
        }

        public bool IsMouseHighlighterEnabled
        {
            get => _isMouseHighlighterEnabled;
            set
            {
                if (_isMouseHighlighterEnabled != value)
                {
                    _isMouseHighlighterEnabled = value;

                    GeneralSettingsConfig.Enabled.MouseHighlighter = value;
                    OnPropertyChanged(nameof(_isMouseHighlighterEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public HotkeySettings MouseHighlighterActivationShortcut
        {
            get
            {
                return MouseHighlighterSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (MouseHighlighterSettingsConfig.Properties.ActivationShortcut != value)
                {
                    MouseHighlighterSettingsConfig.Properties.ActivationShortcut = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public void NotifyMouseHighlighterPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndMouseHighlighterSettings outsettings = new SndMouseHighlighterSettings(MouseHighlighterSettingsConfig);
            SndModuleSettings<SndMouseHighlighterSettings> ipcMessage = new SndModuleSettings<SndMouseHighlighterSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(MouseHighlighterSettingsConfig.ToJsonString(), MouseHighlighterSettings.ModuleName);
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isFindMyMouseEnabled;
        private bool _findMyMouseDoNotActivateOnGameMode;

        private bool _isMouseHighlighterEnabled;
    }
}
