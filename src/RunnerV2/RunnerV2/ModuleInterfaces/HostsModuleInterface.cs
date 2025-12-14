// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class HostsModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.Hosts;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();

        public string Name => "Hosts";

        public override string ProcessPath => string.Empty;

        public override string ProcessName => "PowerToys.Hosts";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SupressLaunchOnModuleEnabled;

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
