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
    internal sealed partial class MouseHighlighterModuleInterface : IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "MouseHighlighter";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.MouseHighlighter;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public void Disable()
        {
            DisableMouseHighlighter();
        }

        public void Enable()
        {
            EnableMouseHighlighter();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<MouseHighlighterSettings>(Name).Properties.ActivationShortcut, ToggleMouseHighlighter));
        }

        public void OnSettingsChanged()
        {
            InitializeShortcuts();
            MouseHighlighterSettingsChanged();
        }

        [LibraryImport("PowerToys.MouseHighlighter.dll")]
        internal static partial void DisableMouseHighlighter();

        [LibraryImport("PowerToys.MouseHighlighter.dll")]
        internal static partial void EnableMouseHighlighter();

        [LibraryImport("PowerToys.MouseHighlighter.dll")]
        internal static partial void MouseHighlighterSettingsChanged();

        [LibraryImport("PowerToys.MouseHighlighter.dll")]
        internal static partial void ToggleMouseHighlighter();
    }
}
