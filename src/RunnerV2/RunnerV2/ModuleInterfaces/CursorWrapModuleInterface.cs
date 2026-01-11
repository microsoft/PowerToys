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
    internal sealed class CursorWrapModuleInterface : IPowerToysModule
    {
        public string Name => "CursorWrap";

        private bool _hookActive;

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.CursorWrap;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredCursorWrapEnabledValue();

        public void Disable()
        {
            _hookActive = false;
            CursorWrapStopMouseHook();
        }

        public void Enable()
        {
            InitializeShortcuts();
            _hookActive = true;
            CursorWrapStartMouseHook();
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<CursorWrapSettings>(Name).Properties.DefaultActivationShortcut, () =>
            {
                if (_hookActive)
                {
                    CursorWrapStopMouseHook();
                    _hookActive = false;
                }
                else
                {
                    CursorWrapStartMouseHook();
                    _hookActive = true;
                }
            }));
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        [DllImport("PowerToys.CursorWrap.dll")]
        private static extern bool CursorWrapStartMouseHook();

        [DllImport("PowerToys.CursorWrap.dll")]
        private static extern bool CursorWrapStopMouseHook();
    }
}
