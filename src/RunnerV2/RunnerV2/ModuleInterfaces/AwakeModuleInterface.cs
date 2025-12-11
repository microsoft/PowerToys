// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;
using Windows.Media.Capture;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class AwakeModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "Awake";

        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.Awake;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAwakeEnabledValue();

        public override string ProcessPath => "PowerToys.Awake.exe";

        public override string ProcessName => "PowerToys.Awake";

        public override string ProcessArguments => $"--use-pt-config --pid {Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess;

        public void Disable()
        {
            InteropEvent terminateEventWrapper = new(InteropEvent.AwakeTerminate);
            terminateEventWrapper.Fire();
            terminateEventWrapper.Dispose();
        }

        public void Enable()
        {
        }
    }
}
