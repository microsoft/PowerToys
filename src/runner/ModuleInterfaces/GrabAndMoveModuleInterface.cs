// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class GrabAndMoveModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "GrabAndMove";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.GrabAndMove;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredGrabAndMoveEnabledValue();

        public override string ProcessPath => "PowerToys.GrabAndMove.exe";

        public override string ProcessName => "PowerToys.GrabAndMove";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Disable()
        {
            ProcessExit();
        }

        public void Enable()
        {
            LaunchProcess(isModuleEnableProcess: true);
        }
    }
}
