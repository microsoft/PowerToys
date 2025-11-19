// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class HostsModuleInterface : IPowerToysModule
    {
        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.Hosts;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();

        public string Name => "Hosts";

        public void Disable()
        {
            foreach (var process in Process.GetProcessesByName("PowerToys.Hosts.exe"))
            {
                process.Kill();
            }
        }

        public void Enable()
        {
        }
    }
}
