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
    internal sealed partial class ColorPickerModuleInterface : IPowerToysModule, IDisposable
    {
        public string Name => "ColorPicker";

        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.ColorPicker;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredColorPickerEnabledValue();

        private Process? _process;

        private InteropEvent? _showUiEventWrapper;

        public void Disable()
        {
            InteropEvent terminateEventWrapper = new(InteropEvent.ColorPickerTerminate);
            terminateEventWrapper.Fire();
            terminateEventWrapper.Dispose();
            _showUiEventWrapper?.Dispose();
            _showUiEventWrapper = null;

            ProcessHelper.ScheudleProcessKill("PowerToys.ColorPickerUI");
        }

        public void Enable()
        {
            if (_process?.HasExited == false)
            {
                return;
            }

            _showUiEventWrapper = new InteropEvent(InteropEvent.ColorPickerShow);

            var psi = new ProcessStartInfo
            {
                FileName = "PowerToys.ColorPickerUI.exe",
                Arguments = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                UseShellExecute = true,
            };

            _process = Process.Start(psi);
        }

        public ColorPickerModuleInterface()
        {
            InitializeHotkey();
        }

        private void InitializeHotkey()
        {
            Shortcuts.Clear();
            Shortcuts.Add((new SettingsUtils().GetSettings<ColorPickerSettings>(Name).Properties.ActivationShortcut, () =>
            {
                if (!_process?.HasExited ?? false)
                {
                    _showUiEventWrapper?.Fire();
                }
            }));
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
            InitializeHotkey();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public void Dispose()
        {
            _process?.Dispose();
            _showUiEventWrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
