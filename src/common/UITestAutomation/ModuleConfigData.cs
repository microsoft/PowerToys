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

    internal class ModuleConfigData
    {
        // URL for Windows Application Driver
        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        // Singleton instance of ModuleConfigData
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => SingletonInstance.Value;

        public Dictionary<PowerToysModuleWindow, string> ModuleWindowName { get; }

        private Dictionary<PowerToysModule, string> ModulePath { get; }

        private ModuleConfigData()
        {
            // Module name and Window name
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, string>
            {
                [PowerToysModuleWindow.PowerToysSettings] = "PowerToys Settings",
                [PowerToysModuleWindow.FancyZone] = "FancyZones Layout",
                [PowerToysModuleWindow.KeyboardManagerKeys] = "Remap keys",
                [PowerToysModuleWindow.KeyboardManagerShortcuts] = "Remap shortcuts",
                [PowerToysModuleWindow.Hosts] = "Hosts File Editor",
            };

            // Exe start path when scope is set to module
            ModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZonesEditor.exe",
                [PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe",
            };
        }

        public string GetModulePath(PowerToysModule scope) => ModulePath[scope];

        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        public string GetModuleWindowData(PowerToysModuleWindow scope) => ModuleWindowName[scope];
    }
}
