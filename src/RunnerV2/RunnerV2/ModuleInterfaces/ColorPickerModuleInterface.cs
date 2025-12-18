// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class ColorPickerModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "ColorPicker";

        public bool Enabled => SettingsUtils.Default.GetSettingsOrDefault<GeneralSettings>().Enabled.ColorPicker;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredColorPickerEnabledValue();

        public void Disable()
        {
            using var terminateEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.TerminateColorPickerSharedEvent());
            terminateEventWrapper.Set();
        }

        public void Enable()
        {
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<ColorPickerSettings>(Name).Properties.ActivationShortcut, () =>
            {
                using var showUiEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent());
                showUiEventWrapper.Set();
            }
            ));
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
            InitializeShortcuts();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public override string ProcessPath => "PowerToys.ColorPickerUI.exe";

        public override string ProcessName => "PowerToys.ColorPickerUI";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;
    }
}
