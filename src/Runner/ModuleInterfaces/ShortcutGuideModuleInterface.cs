// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class ShortcutGuideModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "Shortcut Guide";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.ShortcutGuide;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredShortcutGuideEnabledValue();

        public override string ProcessPath => "PowerToys.ShortcutGuide.exe";

        public override string ProcessName => "PowerToys.ShortcutGuide";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.SupressLaunchOnModuleEnabled;

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<ShortcutGuideSettings>(Name).Properties.OpenShortcutGuide, () =>
            {
                if (Process.GetProcessesByName(ProcessName).Length == 0)
                {
                    LaunchProcess();
                    return;
                }

                ProcessExit();
            }
            ));
        }

        public void Disable()
        {
        }

        public void Enable()
        {
            InitializeShortcuts();
        }

        public void OnSettingsChanged()
        {
            InitializeShortcuts();
        }
    }
}
