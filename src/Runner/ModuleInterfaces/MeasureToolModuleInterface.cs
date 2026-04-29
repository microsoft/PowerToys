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
    internal sealed class MeasureToolModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "Measure Tool";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.MeasureTool;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredScreenRulerEnabledValue();

        public override string ProcessPath => "WinUI3Apps\\PowerToys.MeasureToolUI.exe";

        public override string ProcessName => "PowerToys.MeasureToolUI";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.SupressLaunchOnModuleEnabled;

        public void Disable()
        {
        }

        public void Enable()
        {
            PopulateShortcuts();
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        private void PopulateShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<MeasureToolSettings>(Name).Properties.ActivationShortcut, () =>
                {
                    LaunchProcess();
                }
            ));
        }
    }
}
