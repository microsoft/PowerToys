// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class PowerAccentModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "PowerAccent";

        public bool Enabled => SettingsUtils.Default.GetSettingsOrDefault<GeneralSettings>().Enabled.PowerAccent;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredQuickAccentEnabledValue();

        public override string ProcessPath => "PowerToys.PowerAccent.exe";

        public override string ProcessName => "PowerToys.PowerAccent";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
