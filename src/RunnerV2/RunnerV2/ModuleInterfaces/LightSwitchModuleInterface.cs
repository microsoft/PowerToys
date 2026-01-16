// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;
using Settings.UI.Library;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class LightSwitchModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "LightSwitch";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.LightSwitch;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredLightSwitchEnabledValue();

        public override string ProcessPath => "LightSwitchService\\PowerToys.LightSwitchService.exe";

        public override string ProcessName => "PowerToys.LightSwitchService";

        public override string ProcessArguments => $"--pid {Environment.ProcessId}";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess;

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts
        {
            get => [(SettingsUtils.Default.GetSettings<LightSwitchSettings>(Name).Properties.ToggleThemeHotkey.Value, () =>
            {
                LightSwitchProperties properties = SettingsUtils.Default.GetSettings<LightSwitchSettings>(Name).Properties;
                EnsureLaunched();
                if (properties.ChangeSystem.Value)
                {
                    ThemeHelper.SetSystemTheme(!ThemeHelper.GetCurrentSystemTheme());
                }

                if (properties.ChangeApps.Value)
                {
                    ThemeHelper.SetAppsTheme(!ThemeHelper.GetCurrentAppsTheme());
                }

                using var manualOverrideEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.LightSwitchManualOverrideEvent());
                manualOverrideEvent.Set();
            })];
        }

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
