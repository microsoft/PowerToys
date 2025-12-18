// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class AwakeModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "Awake";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.Awake;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAwakeEnabledValue();

        public override string ProcessPath => "PowerToys.Awake.exe";

        public override string ProcessName => "PowerToys.Awake";

        public override string ProcessArguments => $"--use-pt-config --pid {Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess;

        public void Disable()
        {
            using var terminateEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.AwakeExitEvent());
            terminateEventWrapper.Set();
        }

        public void Enable()
        {
        }
    }
}
