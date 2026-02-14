// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class FindMyMouseModuleInterface : IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "FindMyMouse";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.FindMyMouse;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredFindMyMouseEnabledValue();

        public void Disable()
        {
            FindMyMouseDisable();
        }

        public void Enable()
        {
            InitializeShortcuts();

            var thread = new Thread(() =>
            {
                uint version = 0x00010008;
                int hr = MddBootstrapInitialize(version, 0, IntPtr.Zero);
                if (hr < 0)
                {
                    throw new InvalidOperationException($"Windows app sdk could not be initialized for MouseJump. HR code:{hr}");
                }

                FindMyMouseMain();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void OnSettingsChanged()
        {
            InitializeShortcuts();
            NativeMethods.PostMessageW(GetSonarHwnd(), GetWmPrivSettingsChanged(), IntPtr.Zero, IntPtr.Zero);
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();

            Shortcuts.Add((SettingsUtils.Default.GetSettings<FindMyMouseSettings>(Name).Properties.ActivationShortcut, () =>
            {
                NativeMethods.PostMessageW(GetSonarHwnd(), GetWmPrivShortcut(), IntPtr.Zero, IntPtr.Zero);
            }
            ));
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        [DllImport("PowerToys.FindMyMouse.dll")]
        public static extern void FindMyMouseMain();

        [DllImport("PowerToys.FindMyMouse.dll")]
        public static extern void FindMyMouseDisable();

        [DllImport("PowerToys.FindMyMouse.dll")]
        public static extern IntPtr GetSonarHwnd();

        [DllImport("PowerToys.FindMyMouse.dll")]
        public static extern uint GetWmPrivShortcut();

        [DllImport("PowerToys.FindMyMouse.dll")]
        public static extern uint GetWmPrivSettingsChanged();

        [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", CharSet = CharSet.Unicode)]
        private static extern int MddBootstrapInitialize(uint majorMinorVersion, uint versionTag, IntPtr packageVersion);
    }
}
