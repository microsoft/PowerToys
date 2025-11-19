// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace AlwaysOnTopModuleInterface
{
    public partial class ModuleInterface : IPowerToysModule, IDisposable
    {
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
            _process?.Dispose();
            _pinEventWrapper?.Dispose();
            _pinEventWrapper = null;
        }

        public void Enable()
        {
            _pinEventWrapper = new InteropEvent(InteropEvent.AlwaysOnTopPin);

            var psi = new ProcessStartInfo
            {
                FileName = "PowerToys.AlwaysOnTop.exe",
                Arguments = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                UseShellExecute = true,
            };

            _process = Process.Start(psi);
        }

        public HotkeyEx HotkeyEx => new SettingsUtils().GetSettings<AlwaysOnTopSettings>(Name).Properties.Hotkey.Value;

        public Action OnHotkey => () =>
        {
            if (!_process?.HasExited ?? false)
            {
                _pinEventWrapper?.Fire();
            }
        };

        public void Dispose()
        {
            _process?.Dispose();
            _pinEventWrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
