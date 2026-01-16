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
    internal sealed class WorkspacesModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "Workspaces";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.Workspaces;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredWorkspacesEnabledValue();

        public override string ProcessPath => "PowerToys.WorkspacesEditor.exe";

        public override string ProcessName => "PowerToys.WorkspacesEditor";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SupressLaunchOnModuleEnabled | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Disable()
        {
        }

        public void Enable()
        {
            InitializeShortcuts();
        }

        public void OnSettingsChanged(string settingsKind, System.Text.Json.JsonElement jsonProperties)
        {
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<WorkspacesSettings>(Name).Properties.Hotkey, () =>
            {
                LaunchProcess();
            }));
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];
    }
}
