// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.Utilities;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ScreencastModeViewModel : PageViewModelBase
    {
        protected override string ModuleName => ScreencastModeSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        private ScreencastModeSettings _moduleSettings;
        private bool _isEnabled;

        public ScreencastModeViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _moduleSettings = new ScreencastModeSettings();
            SendConfigMSG = ipcMSGCallBackFunc;

            try
            {
                _moduleSettings = settingsUtils.GetSettingsOrDefault<ScreencastModeSettings>(ScreencastModeSettings.ModuleName);
                if (_moduleSettings == null || _moduleSettings.Properties == null)
                {
                    _moduleSettings = new ScreencastModeSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load ScreencastMode settings: {ex}", ex);
                _moduleSettings = new ScreencastModeSettings();
            }

            try
            {
                _isEnabled = generalSettingsRepository?.SettingsConfig?.Enabled?.ScreencastMode ?? false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get ScreencastMode enabled state: {ex}", ex);
                _isEnabled = false;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    RefreshEnabledState();
                    NotifyPropertyChanged();
                }
            }
        }

        public ScreencastModeSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;
                    NotifyPropertyChanged(nameof(ModuleSettings));
                    RefreshEnabledState();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Logger.LogInfo($"Changed the property {propertyName}");
            OnPropertyChanged(propertyName);
        }

        public void RefreshEnabledState()
        {
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
