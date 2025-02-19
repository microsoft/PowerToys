// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UITestBase")]
[assembly: InternalsVisibleTo("Session")]

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// This file manages the configuration of modules for UI tests.
    /// </summary>
    /// <remarks>
    /// How to add a new module:
    /// 1. Define the new module in the PowerToysModule enum.
    /// 2. Define any associated windows in the PowerToysModuleWindow enum.
    /// 3. Add the exe window name to the ModuleWindowName dictionary in the ModuleConfigData constructor.
    /// 4. If the module has an executable path, add it to the ModulePath dictionary in the ModuleConfigData constructor.
    /// </remarks>

    /// <summary>
    /// Represents the modules in PowerToys.
    /// </summary>
    public enum PowerToysModule
    {
        None,
        FancyZone,
        KeyboardManager,
        Hosts,
    }

    /// <summary>
    /// Represents the windows of PowerToys modules.
    /// One module could have multiple windows.
    /// </summary>
    public enum PowerToysModuleWindow
    {
        None,
        PowerToysSettings,
        FancyZone,
        KeyboardManagerKeys,
        KeyboardManagerShortcuts,
        Hosts,
    }

    internal class ModuleConfigData
    {
        private Dictionary<PowerToysModule, string> ModulePath { get; }

        // Singleton instance of ModuleConfigData.
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => SingletonInstance.Value;

        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        public Dictionary<PowerToysModuleWindow, string> ModuleWindowName { get; }

        private ModuleConfigData()
        {
            // The exe window name for each module.
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, string>
            {
                [PowerToysModuleWindow.PowerToysSettings] = "PowerToys Settings",
                [PowerToysModuleWindow.FancyZone] = "FancyZones Layout",
                [PowerToysModuleWindow.KeyboardManagerKeys] = "Remap keys",
                [PowerToysModuleWindow.KeyboardManagerShortcuts] = "Remap shortcuts",
                [PowerToysModuleWindow.Hosts] = "Hosts File Editor",
            };

            // Exe start path for the module if it exists.
            ModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZonesEditor.exe",
                [PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe",
            };
        }

        public string GetModulePath(PowerToysModule scope) => ModulePath[scope];

        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        public string GetModuleWindowName(PowerToysModuleWindow scope) => ModuleWindowName[scope];
    }
}
