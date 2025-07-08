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
    /// 2. Add the exe window name to the ModuleWindowName dictionary in the ModuleConfigData constructor.
    /// 3. Add the exe path to the ModulePath dictionary in the ModuleConfigData constructor.
    /// </remarks>

    /// <summary>
    /// Represents the modules in PowerToys.
    /// </summary>
    public enum PowerToysModule
    {
        PowerToysSettings,
        FancyZone,
        Hosts,
        Runner,
        Workspaces,
    }

    /// <summary>
    /// Represents the window size for the UI test.
    /// </summary>
    public enum WindowSize
    {
        /// <summary>
        /// Unspecified window size, won't make any size change
        /// </summary>
        UnSpecified,

        /// <summary>
        /// Small window size, 640 * 480
        /// </summary>
        Small,

        /// <summary>
        /// Small window size, 480 * 640
        /// </summary>
        Small_Vertical,

        /// <summary>
        /// Medium window size, 1024 * 768
        /// </summary>
        Medium,

        /// <summary>
        /// Medium window size, 768 * 1024
        /// </summary>
        Medium_Vertical,

        /// <summary>
        /// Large window size, 1920 * 1080
        /// </summary>
        Large,

        /// <summary>
        /// Large window size, 1080 * 1920
        /// </summary>
        Large_Vertical,
    }

    internal class ModuleConfigData
    {
        private Dictionary<PowerToysModule, string> ModulePath { get; }

        // Singleton instance of ModuleConfigData.
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => SingletonInstance.Value;

        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        public Dictionary<PowerToysModule, string> ModuleWindowName { get; }
        
        private bool UseInstallerForTest { get; }

        private ModuleConfigData()
        {
            // Check if we should use installer paths from environment variable
            string useInstallerForTestEnv = Environment.GetEnvironmentVariable("useInstallerForTest") ??
                                          Environment.GetEnvironmentVariable("USEINSTALLERFORTEST");
            UseInstallerForTest = bool.TryParse(useInstallerForTestEnv, out bool result) && result;

            // The exe window name for each module.
            ModuleWindowName = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.PowerToysSettings] = "PowerToys Settings",
                [PowerToysModule.FancyZone] = "FancyZones Layout",
                [PowerToysModule.Hosts] = "Hosts File Editor",
                [PowerToysModule.Runner] = "PowerToys",
                [PowerToysModule.Workspaces] = "Workspaces Editor",
            };

            // Exe start path for the module if it exists.
            ModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.PowerToysSettings] = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe",
                [PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZonesEditor.exe",
                [PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe",
                [PowerToysModule.Runner] = @"\..\..\..\PowerToys.exe",
                [PowerToysModule.Workspaces] = @"\..\..\..\PowerToys.WorkspacesEditor.exe",
            };

            // Installed PowerToys paths
            string powerToysInstallPath = GetPowerToysInstallPath();
            InstalledModulePath = new Dictionary<PowerToysModule, string>
            {
                [PowerToysModule.PowerToysSettings] = Path.Combine(powerToysInstallPath, "WinUI3Apps", "PowerToys.Settings.exe"),
                [PowerToysModule.FancyZone] = Path.Combine(powerToysInstallPath, "PowerToys.FancyZonesEditor.exe"),
                [PowerToysModule.Hosts] = Path.Combine(powerToysInstallPath, "WinUI3Apps", "PowerToys.Hosts.exe"),
                [PowerToysModule.Runner] = Path.Combine(powerToysInstallPath, "PowerToys.exe"),
                [PowerToysModule.Workspaces] = Path.Combine(powerToysInstallPath, "PowerToys.WorkspacesEditor.exe"),
            };
        }

        private string GetPowerToysInstallPath()
        {
            // Try common installation paths
            string[] possiblePaths = {
                @"C:\Program Files\PowerToys",
                @"C:\Program Files (x86)\PowerToys",
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\PowerToys"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerToys")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "PowerToys.exe")))
                {
                    return path;
                }
            }

            // Fallback to Program Files if not found
            return @"C:\Program Files\PowerToys";
        }

        public string GetModulePath(PowerToysModule scope)
        {
            if (UseInstallerForTest)
            {
                string installedPath = InstalledModulePath[scope];
                if (File.Exists(installedPath))
                {
                    return installedPath;
                }
                else
                {
                    // Log warning and fallback to development path
                    Console.WriteLine($"Warning: Installed module not found at {installedPath}, using development path");
                }
            }

            return ModulePath[scope];
        }

        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        public string GetModuleWindowName(PowerToysModule scope) => ModuleWindowName[scope];
    }
}
