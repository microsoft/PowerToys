// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class HostsModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public bool Enabled => SettingsUtils.Default.GetSettingsOrDefault<GeneralSettings>().Enabled.Hosts;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();

        public string Name => "Hosts";

        public override string ProcessPath => "WinUI3Apps\\PowerToys.Hosts.exe";

        public override string ProcessName => "PowerToys.Hosts";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SupressLaunchOnModuleEnabled | ProcessLaunchOptions.NeverExit;

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
