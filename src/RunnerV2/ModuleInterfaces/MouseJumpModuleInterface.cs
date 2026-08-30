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
    internal sealed class MouseJumpModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "MouseJump";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.MouseJump;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredMouseJumpEnabledValue();

        public override string ProcessPath => "PowerToys.MouseJumpUI.exe";

        public override string ProcessName => "PowerToys.MouseJumpUI";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Disable()
        {
            using var terminateEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.TerminateMouseJumpSharedEvent());
            terminateEvent.Set();
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

        public void PopulateShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<MouseJumpSettings>(Name);
            Shortcuts.Add((settings.Properties.ActivationShortcut, () =>
            {
                EnsureLaunched();
                using var invokeMouseJumpEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Constants.MouseJumpShowPreviewEvent());
                invokeMouseJumpEvent.Set();
            }));
        }
    }
}
