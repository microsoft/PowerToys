// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class AlwaysOnTopModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable
    {
        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.AlwaysOnTop;

        public string Name => "AlwaysOnTop";

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();

        private InteropEvent? _pinEventWrapper;

        public void Disable()
        {
            InteropEvent terminateEventWrapper = new(InteropEvent.AlwaysOnTopTerminate);
            terminateEventWrapper.Fire();
            terminateEventWrapper.Dispose();
            _pinEventWrapper?.Dispose();
            _pinEventWrapper = null;
        }

        public void Enable()
        {
            InitializeHotkey();
            _pinEventWrapper = new InteropEvent(InteropEvent.AlwaysOnTopPin);
        }

        private void InitializeHotkey()
        {
            Shortcuts.Clear();
            Shortcuts.Add((new SettingsUtils().GetSettings<AlwaysOnTopSettings>(Name).Properties.Hotkey.Value, () =>
                {
                    _pinEventWrapper?.Fire();
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

        public void Dispose()
        {
            _pinEventWrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
