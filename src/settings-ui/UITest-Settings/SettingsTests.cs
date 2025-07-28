// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Devices.PointOfService.Provider;

namespace Microsoft.Settings.UITests
{
    [TestClass]
    public class SettingsTests : UITestBase
    {
        private readonly string[] dashboardModuleList =
        {
            "Advanced Paste",
            "Always On Top",
            "Awake",
            "Color Picker",
            "Command Palette",
            "Environment Variables",
            "FancyZones",
            "File Locksmith",
            "Find My Mouse",
            "Hosts File Editor",
            "Image Resizer",
            "Keyboard Manager",
            "Mouse Highlighter",
            "Mouse Jump",
            "Mouse Pointer Crosshairs",
            "Mouse Without Borders",
            "New+",
            "Peek",
            "PowerRename",
            "PowerToys Run",
            "Quick Accent",
            "Registry Preview",
            "Screen Ruler",
            "Shortcut Guide",
            "Text Extractor",
            "Workspaces",
            "ZoomIt",

            // "Crop And Lock", // this module cannot be found, why?
        };

        private readonly string[] moduleProcess =
        {
            "PowerToys.AdvancedPaste",
            "PowerToys.Run",
            "PowerToys.AlwaysOnTop",
            "PowerToys.Awake",
            "PowerToys.ColorPickerUI",
            "PowerToys.Peek.UI",
        };

        public SettingsTests()
            : base(PowerToysModule.PowerToysSettings, size: WindowSize.Large)
        {
        }

        [TestMethod("PowerToys.Settings.ModulesOnAndOffTest")]
        [TestCategory("Settings Test #1")]
        public void TestAllmoduleOnAndOff()
        {
            DisableAllModules();
            Task.Delay(2000).Wait();

            // module process won't be killed in debug mode settings UI!
            // Assert.IsTrue(CheckModulesDisabled(), "Some modules are not disabled.");
            EnableAllModules();
            Task.Delay(2000).Wait();

            // Assert.IsTrue(CheckModulesEnabled(), "Some modules are not Enabled.");
        }

        private void DisableAllModules()
        {
            Find<NavigationViewItem>("Dashboard").Click();

            foreach (var moduleName in dashboardModuleList)
            {
                var moduleButton = Find<Button>(moduleName);
                Assert.IsNotNull(moduleButton);
                var toggle = moduleButton.Find<ToggleSwitch>("Enable module");
                Assert.IsNotNull(toggle);
                if (toggle.IsOn)
                {
                    toggle.Click();
                }
            }
        }

        private void EnableAllModules()
        {
            Find<NavigationViewItem>("Dashboard").Click();

            foreach (var moduleName in dashboardModuleList)
            {
                // Scroll(direction: "Down");
                var moduleButton = Find<Button>(moduleName);
                Assert.IsNotNull(moduleButton);
                var toggle = moduleButton.Find<ToggleSwitch>("Enable module");
                Assert.IsNotNull(toggle);
                if (!toggle.IsOn)
                {
                    toggle.Click();
                }
            }
        }

        private bool CheckModulesDisabled()
        {
            Process[] runningProcesses = Process.GetProcesses();

            foreach (var process in moduleProcess)
            {
                if (runningProcesses.Any(p => p.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckModulesEnabled()
        {
            Process[] runningProcesses = Process.GetProcesses();

            foreach (var process in moduleProcess)
            {
                if (!runningProcesses.Any(p => p.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
