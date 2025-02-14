// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.UITest
{
    public enum PowerToysModule
    {
        None,
        FancyZone,
        KeyboardManager,
        Hosts,
    }

    public enum PowerToysModuleWindow
    {
        None,
        PowerToysSettings,
        FancyZone,
        KeyboardManagerKeys,
        KeyboardManagerShortcuts,
        Hosts,
    }

    public class ModuleConfigData
    {
        // URL for Windows Application Driver
        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        // Singleton instance of ModuleConfigData
        private static readonly Lazy<ModuleConfigData> MInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => MInstance.Value;

        // Dictionary to hold module window names
        public Dictionary<PowerToysModuleWindow, ModuleWindowData> ModuleWindowName { get; }

        // Dictionary to hold module paths
        private Dictionary<PowerToysModule, string> ModulePath { get; }

        // Private constructor to initialize module data
        private ModuleConfigData()
        {
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, ModuleWindowData>
            {
                [PowerToysModuleWindow.PowerToysSettings] = new ModuleWindowData("PowerToys", "PowerToys Settings"),
                [PowerToysModuleWindow.FancyZone] = new ModuleWindowData("Fancyzone", "FancyZones Layout"),
                [PowerToysModuleWindow.KeyboardManagerKeys] = new ModuleWindowData("KeyboardManagerKeys", "Remap keys"),
                [PowerToysModuleWindow.KeyboardManagerShortcuts] = new ModuleWindowData("KeyboardManagerShortcuts", "Remap shortcuts"),
                [PowerToysModuleWindow.Hosts] = new ModuleWindowData("Hosts", "Hosts File Editor"),
            };

            ModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZones.exe",
                [PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe",
            };
        }

        // Method to get the path of a module
        public string GetModulePath(PowerToysModule scope) => ModulePath[scope];

        // Method to get the URL of the Windows Application Driver
        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        // Method to get the window data of a module
        public ModuleWindowData GetModuleWindowData(PowerToysModuleWindow scope) => ModuleWindowName[scope];
    }

    // Struct to hold module window data
    public struct ModuleWindowData
    {
        public string ModuleName { get; set; }

        public string WindowName { get; set; }

        // Constructor to initialize module window data
        public ModuleWindowData(string moduleName, string windowName)
        {
            ModuleName = moduleName;
            WindowName = windowName;
        }
    }
}
