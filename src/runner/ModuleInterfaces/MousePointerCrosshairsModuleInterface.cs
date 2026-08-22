// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class MousePointerCrosshairsModuleInterface : IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "MousePointerCrosshairs";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.MousePointerCrosshairs;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();

        public void Disable()
        {
            DisableMousePointerCrosshairs();
        }

        public void Enable()
        {
            InitializeShortcuts();
            EnableMousePointerCrosshairs();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<MousePointerCrosshairsSettings>(Name).Properties;
            Shortcuts.Add((settings.ActivationShortcut, OnMousePointerCrosshairsActivationShortcut));
            Shortcuts.Add((settings.GlidingCursorActivationShortcut, OnMousePointerCrosshairsGlidingCursorShortcut));
        }

        public void OnSettingsChanged()
        {
            OnMousePointerCrosshairsSettingsChanged();
            InitializeShortcuts();
        }

        [LibraryImport("PowerToys.MousePointerCrosshairs.dll")]
        internal static partial void DisableMousePointerCrosshairs();

        [LibraryImport("PowerToys.MousePointerCrosshairs.dll")]
        internal static partial void EnableMousePointerCrosshairs();

        [LibraryImport("PowerToys.MousePointerCrosshairs.dll")]
        internal static partial void OnMousePointerCrosshairsSettingsChanged();

        [LibraryImport("PowerToys.MousePointerCrosshairs.dll")]
        internal static partial void OnMousePointerCrosshairsActivationShortcut();

        [LibraryImport("PowerToys.MousePointerCrosshairs.dll")]
        internal static partial void OnMousePointerCrosshairsGlidingCursorShortcut();
    }
}
