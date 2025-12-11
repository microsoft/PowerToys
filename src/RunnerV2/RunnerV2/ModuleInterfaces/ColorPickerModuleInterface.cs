// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class ColorPickerModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable
    {
        public string Name => "ColorPicker";

        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.ColorPicker;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredColorPickerEnabledValue();

        private InteropEvent? _showUiEventWrapper;

        public void Disable()
        {
            InteropEvent terminateEventWrapper = new(InteropEvent.ColorPickerTerminate);
            terminateEventWrapper.Fire();
            terminateEventWrapper.Dispose();
            _showUiEventWrapper?.Dispose();
            _showUiEventWrapper = null;
        }

        public void Enable()
        {
            InitializeShortcuts();

            _showUiEventWrapper = new InteropEvent(InteropEvent.ColorPickerShow);
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((new SettingsUtils().GetSettings<ColorPickerSettings>(Name).Properties.ActivationShortcut, () =>
            {
                _showUiEventWrapper?.Fire();
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

        public void Dispose()
        {
            _showUiEventWrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
