// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class EnvironmentVariablesModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "Environment Variables";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.EnvironmentVariables;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue();

        public override string ProcessPath => "WinUI3Apps\\PowerToys.EnvironmentVariables.exe";

        public override string ProcessName => "PowerToys.EnvironmentVariables";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SupressLaunchOnModuleEnabled | ProcessLaunchOptions.NeverExit;

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
