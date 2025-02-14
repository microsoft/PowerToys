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

        public Dictionary<PowerToysModuleWindow, ModuleWindowData> ModuleWindowName { get; }

        private Dictionary<PowerToysModule, string> ModulePath { get; }

        private ModuleConfigData()
        {
            // Module name and Window name
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, ModuleWindowData>
            {
                [PowerToysModuleWindow.PowerToysSettings] = new ModuleWindowData("PowerToys", "PowerToys Settings"),
                [PowerToysModuleWindow.FancyZone] = new ModuleWindowData("Fancyzone", "FancyZones Layout"),
                [PowerToysModuleWindow.KeyboardManagerKeys] = new ModuleWindowData("KeyboardManagerKeys", "Remap keys"),
                [PowerToysModuleWindow.KeyboardManagerShortcuts] = new ModuleWindowData("KeyboardManagerShortcuts", "Remap shortcuts"),
                [PowerToysModuleWindow.Hosts] = new ModuleWindowData("Hosts", "Hosts File Editor"),
            };

            // Exe start path when scope is set to module
            ModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZones.exe",
                [PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe",
            };
        }

        public string GetModulePath(PowerToysModule scope) => ModulePath[scope];

        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        public ModuleWindowData GetModuleWindowData(PowerToysModuleWindow scope) => ModuleWindowName[scope];
    }

    public struct ModuleWindowData
    {
        public string ModuleName { get; set; }

        public string WindowName { get; set; }

        public ModuleWindowData(string moduleName, string windowName)
        {
            ModuleName = moduleName;
            WindowName = windowName;
        }
    }
}
