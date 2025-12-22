// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class PowerToysRunModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "PowerToys Run";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.PowerLauncher;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredPowerLauncherEnabledValue();

        public override string ProcessPath => Path.GetFullPath("PowerToys.PowerLauncher.exe");

        public override string ProcessName => "PowerToys.PowerLauncher";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.ElevateIfApplicable | ProcessLaunchOptions.SingletonProcess;

        public override string ProcessArguments => $"-powerToysPid {Environment.ProcessId}";

        public void Disable()
        {
            using var terminateEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.RunExitEvent());
            terminateEvent.Set();
        }

        public void Enable()
        {
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts =>
        [
            (
                SettingsUtils.Default.GetSettings<PowerLauncherSettings>(Name).Properties.OpenPowerLauncher,
                () =>
                {
                    EnsureLaunched();
                    using var invokeRunEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.PowerLauncherCentralizedHookSharedEvent());
                    invokeRunEvent.Set();
                }
            ),
        ];
    }
}
