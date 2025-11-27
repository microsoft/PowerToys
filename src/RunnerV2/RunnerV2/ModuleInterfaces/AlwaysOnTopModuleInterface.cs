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

namespace RunnerV2.ModuleInterfaces
{
    public partial class AlwaysOnTopModuleInterface : IPowerToysModule, IDisposable
    {
        private static readonly ushort _pinHotkeyAtom = NativeMethods.AddAtomW("PowerToys_AlwaysOnTop_PinHotkey");

        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.AlwaysOnTop;

        public string Name => "AlwaysOnTop";

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();

        private Process? _process;

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
            if (_process?.HasExited == false)
            {
                return;
            }

            _pinEventWrapper = new InteropEvent(InteropEvent.AlwaysOnTopPin);

            var psi = new ProcessStartInfo
            {
                FileName = "PowerToys.AlwaysOnTop.exe",
                Arguments = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                UseShellExecute = true,
            };

            _process = Process.Start(psi);
        }

        public AlwaysOnTopModuleInterface()
        {
            InitializeHotkey();
        }

        private void InitializeHotkey()
        {
            Hotkeys.Clear();
            Hotkeys.Add(((HotkeyEx)new SettingsUtils().GetSettings<AlwaysOnTopSettings>(Name).Properties.Hotkey.Value) with { Identifier = _pinHotkeyAtom }, () =>
            {
                if (!_process?.HasExited ?? false)
                {
                    _pinEventWrapper?.Fire();
                }
            });
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
            InitializeHotkey();
        }

        public Dictionary<HotkeyEx, Action> Hotkeys { get; } = [];

        public void Dispose()
        {
            _process?.Dispose();
            _pinEventWrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
