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
    public enum PowerToysModule
    {
        None,
        FancyZone,
        KeyboardManager,
        Hosts,
    }

    internal class ModuleConfigData
    {
        // Mapping module to exe path
        private Dictionary<PowerToysModule, string> ModulePath { get; }

        // Singleton instance of ModuleConfigData
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        // URL for Windows Application Driver
        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        public static ModuleConfigData Instance => SingletonInstance.Value;

        // Function
        private ModuleConfigData()
        {
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
    }
}
