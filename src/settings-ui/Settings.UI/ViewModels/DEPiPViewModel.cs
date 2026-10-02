// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DEPiPViewModel : PageViewModelBase
    {
        protected override string ModuleName => "DEPiP";

        private GeneralSettings GeneralSettingsConfig { get; }

        private Func<string, int> SendConfigMSG { get; }

        private DEPiPSettings ModuleSettings { get; }

        private bool _isEnabled;

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public DEPiPViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            DEPiPSettings moduleSettings,
            Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            ModuleSettings = moduleSettings ?? new DEPiPSettings();
            SendConfigMSG = ipcMSGCallBackFunc ?? (_ => 0);
            InitializeEnabledValue();
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.DEPiP = value;
                    SendConfigMSG(new OutGoingGeneralSettings(GeneralSettingsConfig).ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        public void Launch()
        {
            if (App.PowerToysPID != 0)
            {
                NativeMethods.AllowSetForegroundWindow(App.PowerToysPID);
            }

            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowDEPiPSharedEvent());
            eventHandle.Set();
        }

        public int InactiveTransparency
        {
            get => ModuleSettings.Properties.InactiveTransparency.Value;
            set
            {
                int clampedValue = Math.Clamp(value, 0, 90);
                if (ModuleSettings.Properties.InactiveTransparency.Value != clampedValue)
                {
                    ModuleSettings.Properties.InactiveTransparency.Value = clampedValue;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(InactiveTransparency));
                }
            }
        }

        public bool LockAspectRatio
        {
            get => ModuleSettings.Properties.LockAspectRatio.Value;
            set
            {
                if (ModuleSettings.Properties.LockAspectRatio.Value != value)
                {
                    ModuleSettings.Properties.LockAspectRatio.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(LockAspectRatio));
                }
            }
        }

        public bool AlwaysOnTop
        {
            get => ModuleSettings.Properties.AlwaysOnTop.Value;
            set
            {
                if (ModuleSettings.Properties.AlwaysOnTop.Value != value)
                {
                    ModuleSettings.Properties.AlwaysOnTop.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(AlwaysOnTop));
                }
            }
        }

        private void NotifyModuleSettingsChanged()
        {
            SndDEPiPSettings settings = new(ModuleSettings);
            SendConfigMSG(new SndModuleSettings<SndDEPiPSettings>(settings).ToJsonString());
        }

        private void InitializeEnabledValue()
        {
            _isEnabled = GeneralSettingsConfig.Enabled.DEPiP;
        }
    }
}
