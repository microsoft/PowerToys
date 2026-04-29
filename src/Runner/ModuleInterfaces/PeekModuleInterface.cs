// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Helpers;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class PeekModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        private static readonly nint PeekDllHandle = LoadPeekDll();

        private static nint LoadPeekDll()
        {
            // Pin the DLL in memory for the life of the Runner process, because it owns WinEvent hook callbacks.
            var dllPath = Path.Combine(AppContext.BaseDirectory, "WinUI3Apps", "PowerToys.Peek.dll");
            return NativeLibrary.Load(dllPath);
        }

        private readonly EventWaitHandle terminatePeekEvent = new(false, EventResetMode.AutoReset, Constants.TerminatePeekEvent());

        public string Name => "Peek";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.Peek;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredPeekEnabledValue();

        public override string ProcessPath => "WinUI3Apps\\PowerToys.Peek.UI.exe";

        public override string ProcessName => "PowerToys.Peek.UI";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.SingletonProcess | (SettingsUtils.Default.GetSettings<PeekSettings>(Name).Properties.AlwaysRunNotElevated.Value ? ProcessLaunchOptions.NeverElevate : 0);

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public void Disable()
        {
            terminatePeekEvent.Set();
            PeekSetForegroundHookActive(false);
            PeekManageSpaceModeHook();
        }

        public void Dispose()
        {
            terminatePeekEvent.Dispose();
            GC.SuppressFinalize(this);
        }

        // Todo: Implement always launch non-elevated
        public void Enable()
        {
            OnSettingsChanged();
        }

        private void PopulateShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<PeekSettings>(Name).Properties;
            HotkeySettings hotkey = settings.EnableSpaceToActivate.Value ? new HotkeySettings(false, false, false, false, 0x20) : settings.DefaultActivationShortcut;
            Shortcuts.Add((hotkey, () =>
            {
                EnsureLaunched();

                PeekOnHotkey();
            }
            ));
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
            PeekSetForegroundHookActive(SettingsUtils.Default.GetSettings<PeekSettings>(Name).Properties.EnableSpaceToActivate.Value);
            PeekManageSpaceModeHook();
        }

        [LibraryImport("WinUI3Apps\\PowerToys.Peek.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PeekOnHotkey();

        [LibraryImport("WinUI3Apps\\PowerToys.Peek.dll")]
        private static partial void PeekSetForegroundHookActive([MarshalAs(UnmanagedType.Bool)] bool active);

        [LibraryImport("WinUI3Apps\\PowerToys.Peek.dll")]
        private static partial void PeekManageSpaceModeHook();
    }
}
