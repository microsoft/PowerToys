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

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class AwakeModuleInterface : IPowerToysModule, IDisposable
    {
        public string Name => "Awake";

        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.Awake;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAwakeEnabledValue();

        private Process? _process;

        public void Disable()
        {
            InteropEvent terminateEventWrapper = new(InteropEvent.AwakeTerminate);
            terminateEventWrapper.Fire();
            terminateEventWrapper.Dispose();

            ProcessHelper.ScheudleProcessKill("PowerToys.Awake");
        }

        public void Enable()
        {
            if (_process?.HasExited == false)
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "PowerToys.Awake.exe",
                Arguments = $"--use-pt-config --pid {Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}",
                UseShellExecute = true,
            };

            _process = Process.Start(psi);
        }

        public void Dispose()
        {
            _process?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
