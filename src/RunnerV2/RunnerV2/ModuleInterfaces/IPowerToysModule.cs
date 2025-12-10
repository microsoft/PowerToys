// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace RunnerV2.ModuleInterfaces
{
    public interface IPowerToysModule
    {
        /// <summary>
        /// Gets the short name of the module. The same used as the name of the folder containing its settings.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// This function is called when the module is enabled.
        /// </summary>
        public void Enable();

        /// <summary>
        /// This function is called when the module is disabled.
        /// </summary>
        public void Disable();

        /// <summary>
        /// Gets a value indicating whether the module is enabled.
        /// </summary>
        /// <remarks>
        /// This value shall be read from the settings of the module in the module interface implementation.
        /// </remarks>
        public bool Enabled { get; }

        /// <summary>
        /// Gets the GPO rule configured state for the module.
        /// </summary>
        /// <remarks>
        /// This value shall be read from the GPO settings with the <see cref="GPOWrapper"/> class.
        /// </remarks>
        public GpoRuleConfigured GpoRuleConfigured { get; }

        /// <summary>
        /// Gets a dictionary of hotkeys and their associated actions. Every hotkey must have an associated id.
        /// </summary>
        /// <remarks>
        /// If this property is not overridden, the module is considered to not have hotkeys.
        /// </remarks>
        public Dictionary<HotkeyEx, Action> Hotkeys { get => []; }

        /// <summary>
        /// Gets a list of shortcuts, that shall be registered in the keyboard hook, and their associated actions.
        /// </summary>
        /// <remarks>
        /// If this property is not overridden, the module is considered to not have shortcuts.
        /// </remarks>
        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get => []; }

        public Dictionary<string, Action> CustomActions { get => []; }

        /// <summary>
        /// This function is called when the settings of the module or the general settings are changed.
        /// </summary>
        /// <param name="settingsKind">Value of <see cref="Name"/> or "general" indicating the type of change.</param>
        /// <param name="jsonProperties">The json element with the new settings.</param>
        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
        }
    }
}
