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
    // How to add a new module:
    // 1. Define the new module in the PowerToysModule enum.
    // 2. Define any associated windows in the PowerToysModuleWindow enum.
    // 3. Add the window names to the ModuleWindowName dictionary in the ModuleConfigData constructor.
    // 4. If the module has an executable path, add it to the ModulePath dictionary in the ModuleConfigData constructor.

    // Represents the modules in PowerToys.
    // Add a new module:
    public enum PowerToysModule
    {
        None,
        FancyZone,
        KeyboardManager,
        Hosts,
    }

    // Represents the windows of PowerToys modules.
    // One module could have multiple windows.
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
        // URL for Windows Application Driver
        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        // Singleton instance of ModuleConfigData
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => SingletonInstance.Value;

        // Mapping window to string name
        public Dictionary<PowerToysModuleWindow, string> ModuleWindowName { get; }

        // Mapping module to exe path
        private Dictionary<PowerToysModule, string> ModulePath { get; }

        private ModuleConfigData()
        {
            // Set a string window name for each module.
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

        // Gets the executable path for the specified PowerToys module.
        // Parameters:
        //   scope: The PowerToys module.
        // Returns: The exe path for the specified module.
        public string GetModulePath(PowerToysModule scope) => ModulePath[scope];

        // Gets the URL for the Windows Application Driver.
        // Returns: The URL for the Windows Application Driver.
        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        // Gets the window name for the specified PowerToys module window.
        // Parameters:
        //   scope: The PowerToys module window.
        // Returns: The window name for the specified module window.
        public string GetModuleWindowData(PowerToysModuleWindow scope) => ModuleWindowName[scope];
    }
}
