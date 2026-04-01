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
        PowerRename,
        CommandPalette,
        ScreenRuler,
        LightSwitch,
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
        private Dictionary<PowerToysModule, ModuleInfo> ModuleInfo { get; }

        // Singleton instance of ModuleConfigData.
        private static readonly Lazy<ModuleConfigData> SingletonInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance => SingletonInstance.Value;

        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        private bool UseInstallerForTest { get; }

        private ModuleConfigData()
        {
            // Check if we should use installer paths from environment variable
            UseInstallerForTest = EnvironmentConfig.UseInstallerForTest;

            // Module information including executable name, window name, and optional subdirectory
            ModuleInfo = new Dictionary<PowerToysModule, ModuleInfo>
            {
                [PowerToysModule.PowerToysSettings] = new ModuleInfo("PowerToys.Settings.exe", "PowerToys Settings", "WinUI3Apps"),
                [PowerToysModule.FancyZone] = new ModuleInfo("PowerToys.FancyZonesEditor.exe", "FancyZones Layout"),
                [PowerToysModule.Hosts] = new ModuleInfo("PowerToys.Hosts.exe", "Hosts File Editor", "WinUI3Apps"),
                [PowerToysModule.Runner] = new ModuleInfo("PowerToys.exe", "PowerToys"),
                [PowerToysModule.Workspaces] = new ModuleInfo("PowerToys.WorkspacesEditor.exe", "Workspaces Editor"),
                [PowerToysModule.PowerRename] = new ModuleInfo("PowerToys.PowerRename.exe", "PowerRename", "WinUI3Apps"),
                [PowerToysModule.CommandPalette] = new ModuleInfo("Microsoft.CmdPal.UI.exe", "PowerToys Command Palette", "WinUI3Apps\\CmdPal"),
                [PowerToysModule.ScreenRuler] = new ModuleInfo("PowerToys.MeasureToolUI.exe", "PowerToys.ScreenRuler", "WinUI3Apps"),
                [PowerToysModule.LightSwitch] = new ModuleInfo("PowerToys.LightSwitch.exe", "PowerToys.LightSwitch", "LightSwitchService"),
            };
        }

        private string GetPowerToysInstallPath()
        {
            // Try common installation paths
            string[] possiblePaths =
            {
                @"C:\Program Files\PowerToys",
                @"C:\Program Files (x86)\PowerToys",
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\PowerToys"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerToys"),
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
            var moduleInfo = ModuleInfo[scope];

            if (UseInstallerForTest)
            {
                string powerToysInstallPath = GetPowerToysInstallPath();
                string installedPath = moduleInfo.GetInstalledPath(powerToysInstallPath);

                if (File.Exists(installedPath))
                {
                    return installedPath;
                }
                else
                {
                    Console.WriteLine($"Warning: Installed module not found at {installedPath}, using development path");
                }
            }

            return moduleInfo.GetDevelopmentPath();
        }

        public string GetWindowsApplicationDriverUrl() => WindowsApplicationDriverUrl;

        public string GetModuleWindowName(PowerToysModule scope) => ModuleInfo[scope].WindowName;
    }
}
