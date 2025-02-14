// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        PowerToys,
        FancyZone,
        KeyboardManagerKeys,
        KeyboardManagerShortcuts,
        Hosts,
    }

    public class ModuleConfigData
    {
        public const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        private static readonly Lazy<ModuleConfigData> MInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance
        {
            get
            {
                return MInstance.Value;
            }
        }

        public Dictionary<PowerToysModuleWindow, ModuleWindowData> ModuleWindowName { get; private set; }

        private Dictionary<PowerToysModule, string> ModulePath { get; set; }

        private ModuleConfigData()
        {
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, ModuleWindowData>();
            ModuleWindowName[PowerToysModuleWindow.FancyZone] = new ModuleWindowData("PowerToys", "PowerToys Settings");
            ModuleWindowName[PowerToysModuleWindow.FancyZone] = new ModuleWindowData("Fancyzone", "FancyZones Layout");
            ModuleWindowName[PowerToysModuleWindow.KeyboardManagerKeys] = new ModuleWindowData("KeyboardManagerKeys", "Remap keys");
            ModuleWindowName[PowerToysModuleWindow.KeyboardManagerShortcuts] = new ModuleWindowData("KeyboardManagerShortcuts", "Remap shortcuts");
            ModuleWindowName[PowerToysModuleWindow.Hosts] = new ModuleWindowData("Hosts", "Hosts File Editor");

            ModulePath = new Dictionary<PowerToysModule, string>();
            ModulePath[PowerToysModule.FancyZone] = @"\..\..\..\PowerToys.FancyZones.exe";
            ModulePath[PowerToysModule.Hosts] = @"\..\..\..\WinUI3Apps\PowerToys.Hosts.exe";
        }

        public string GetModulePath(PowerToysModule scope)
        {
            return ModulePath[scope];
        }

        public string GetWindowsApplicationDriverUrl()
        {
            return WindowsApplicationDriverUrl;
        }

        public ModuleWindowData GetModuleWindowData(PowerToysModuleWindow scope)
        {
            return ModuleWindowName[scope];
        }
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
