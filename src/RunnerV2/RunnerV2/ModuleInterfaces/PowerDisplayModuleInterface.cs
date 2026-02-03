// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class PowerDisplayModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleCustomActionsProvider, IDisposable
    {
        public string Name => "PowerDisplay";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.PowerDisplay;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredPowerDisplayEnabledValue();

        public override string ProcessPath => "WinUI3Apps\\PowerToys.PowerDisplay.exe";

        public override string ProcessName => "PowerToys.PowerDisplay.exe";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        private static readonly string _outputPipeName = "powertoys_power_display_" + Guid.NewGuid();
        private readonly TwoWayPipeMessageIPCManaged _ipc = new("\\\\.\\pipe\\powertoys_power_display_input", "\\\\.\\pipe\\" + _outputPipeName, (_) => { });

        public override string ProcessArguments => _outputPipeName;

        public void Disable()
        {
            _ipc.Send(Constants.PowerDisplayTerminateAppMessage());
            _ipc.End();
        }

        public void Enable()
        {
            _ipc.Start();
        }

        public void Dispose()
        {
            _ipc.Dispose();
            GC.SuppressFinalize(this);
        }

        public Dictionary<string, Action> CustomActions => new()
        {
            { "Launch", () => { _ipc.Send(Constants.PowerDisplayToggleMessage()); } },
            { "RefreshMonitors", () => { _ipc.Send(Constants.RefreshPowerDisplayMonitorsEvent()); } },
            { "ApplyProfile", () => { _ipc.Send(Constants.PowerDisplayApplyProfileMessage()); } },
        };
    }
}
