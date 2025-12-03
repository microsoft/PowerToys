// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class HostsModuleInterface : IPowerToysModule
    {
        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.Hosts;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();

        public string Name => "Hosts";

        public void Disable()
        {
            ProcessHelper.ScheudleProcessKill("PowerToys.Hosts", 0);
        }

        public void Enable()
        {
        }
    }
}
