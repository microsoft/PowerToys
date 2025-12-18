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
    internal sealed class AlwaysOnTopModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.AlwaysOnTop;

        public string Name => "AlwaysOnTop";

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();

        public void Disable()
        {
            using var terminateEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.AlwaysOnTopTerminateEvent());
            terminateEventWrapper.Set();
        }

        public void Enable()
        {
            InitializeHotkey();
        }

        private void InitializeHotkey()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<AlwaysOnTopSettings>(Name).Properties.Hotkey.Value, () =>
                {
                    using var pinEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.AlwaysOnTopPinEvent());
                    pinEventWrapper.Set();
                }
            ));
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
            InitializeHotkey();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public override string ProcessPath => "PowerToys.AlwaysOnTop.exe";

        public override string ProcessName => "PowerToys.AlwaysOnTop";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.ElevateIfApplicable;
    }
}
