// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class AltWindowCycleModuleInterface : IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "AltWindowCycle";

        private bool _initialized;

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.AltWindowCycle;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAltWindowCycleEnabledValue();

        public void Disable()
        {
            if (_initialized)
            {
                ShutdownAltWindowCycle(false);
                _initialized = false;
            }
        }

        public void Enable()
        {
            InitializeShortcuts();
            _initialized = AltWindowCycleInitialize();
        }

        public void OnSettingsChanged()
        {
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<AltWindowCycleSettings>(Name);
            Shortcuts.Add((settings.Properties.NextWindowShortcut, () =>
            {
                AltWindowCycleOnNextHotkey();
            }));
            Shortcuts.Add((settings.Properties.PreviousWindowShortcut, () =>
            {
                AltWindowCycleOnPreviousHotkey();
            }));
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        [DllImport("PowerToys.AltWindowCycle.dll", EntryPoint = "InitializeAltWindowCycle")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AltWindowCycleInitialize();

        [DllImport("PowerToys.AltWindowCycle.dll", EntryPoint = "ShutdownAltWindowCycle")]
        private static extern void ShutdownAltWindowCycle([MarshalAs(UnmanagedType.Bool)] bool blockUntilExit);

        [DllImport("PowerToys.AltWindowCycle.dll", EntryPoint = "HandleAltWindowCycleHotkey")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AltWindowCycleHandleHotkey([MarshalAs(UnmanagedType.Bool)] bool forward, uint holdModifiers);

        private static void AltWindowCycleOnNextHotkey()
        {
            AltWindowCycleHandleHotkey(true, 0);
        }

        private static void AltWindowCycleOnPreviousHotkey()
        {
            AltWindowCycleHandleHotkey(false, 0);
        }
    }
}
