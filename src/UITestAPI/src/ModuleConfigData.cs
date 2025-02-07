// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.UITests.API
{
    public enum PowerToysModule
    {
        None,
        Fancyzone,
        KeyboardManager,
        Hosts,
    }

    public enum PowerToysModuleWindow
    {
        None,
        Fancyzone,
        KeyboardManagerKeys,
        KeyboardManagerShortcuts,
        Hosts,
    }

    public class ModuleConfigData
    {
        private static readonly Lazy<ModuleConfigData> MInstance = new Lazy<ModuleConfigData>(() => new ModuleConfigData());

        public static ModuleConfigData Instance
        {
            get
            {
                return MInstance.Value;
            }
        }

        public Dictionary<PowerToysModuleWindow, ModuleWindowData> ModuleWindowName { get; private set; }

        private ModuleConfigData()
        {
            ModuleWindowName = new Dictionary<PowerToysModuleWindow, ModuleWindowData>();
            ModuleWindowName[PowerToysModuleWindow.Fancyzone] = new ModuleWindowData("Fancyzone", "FancyZones Layout");
            ModuleWindowName[PowerToysModuleWindow.KeyboardManagerKeys] = new ModuleWindowData("KeyboardManagerKeys", "Remap keys");
            ModuleWindowName[PowerToysModuleWindow.KeyboardManagerShortcuts] = new ModuleWindowData("KeyboardManagerShortcuts", "Remap shortcuts");
            ModuleWindowName[PowerToysModuleWindow.Hosts] = new ModuleWindowData("Hosts", "Hosts File Editor");
        }
    }

    public struct ModuleWindowData(string moduleName, string windowName)
    {
        public string ModuleName = moduleName;
        public string WindowName = windowName;
    }
}
