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
    internal sealed class FancyZonesModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleCustomActionsProvider, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "FancyZones";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.FancyZones;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredFancyZonesEnabledValue();

        public override string ProcessPath => "PowerToys.FancyZones.exe";

        public override string ProcessName => "PowerToys.FancyZones";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.HideUI;

        public void Disable()
        {
            using var terminateEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.FZEExitEvent());
            terminateEvent.Set();
        }

        public void Enable()
        {
            InitializeShortcuts();
        }

        public Dictionary<string, Action<string>> CustomActions => new()
        {
            {
                "ToggledFZEditor",
                (_) =>
                {
                    EnsureLaunched();
                    using var invokeFZEditorEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.FZEToggleEvent());
                    invokeFZEditorEvent.Set();
                }
            },
        };

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public void InitializeShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<FancyZonesSettings>(Name);
            Shortcuts.Add((settings.Properties.FancyzonesEditorHotkey.Value, () =>
            {
                EnsureLaunched();
                using var invokeFZEditorEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.FZEToggleEvent());
                invokeFZEditorEvent.Set();
            }
            ));
        }

        public void OnSettingsChanged()
        {
            InitializeShortcuts();
        }
    }
}
