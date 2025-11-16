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
    public class ModuleInterface : IPowerToysModule
    {
        public bool Enabled => true;

        public string Name => "AlwaysOnTop";

        public GpoRuleConfigured GpoRuleConfigured => GpoRuleConfigured.Unavailable;

        private Process? _process;

        private IntPtr pinEvent = CreateEventW(IntPtr.Zero, false, false, "Local\\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8");

        public void Disable()
        {
            if (_process is not null && !_process.HasExited)
            {
                _process.Kill();
            }

            if (pinEvent != IntPtr.Zero)
            {
                CloseHandle(pinEvent);
                pinEvent = IntPtr.Zero;
            }
        }

        public void Enable()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "PowerToys.AlwaysOnTop.exe",
                Arguments = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                UseShellExecute = true,
            };

            _process = Process.Start(psi);
        }

        public HotkeyEx HotkeyEx => new(0x2 | 0x1, 0x54); // Ctrl + Alt + T

        public Action OnHotkey => () =>
        {
            if (_process is not null && !_process.HasExited && pinEvent != IntPtr.Zero)
            {
                _ = SetEvent(pinEvent);
            }
        };

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetEvent(IntPtr hEvent);
    }
}
