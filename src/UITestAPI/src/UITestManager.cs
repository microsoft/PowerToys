// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.UITests.API.ModuleConfigData;
using static Microsoft.UITests.API.UITestBase;

namespace Microsoft.UITests.API
{
    public class UITestManager
    {
        protected const string PowerToysPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

        private static Process? appDriver;

        public UITestManager()
        {
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public static void Init(string winAppDriverPath = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe")
        {
            appDriver = Process.Start(winAppDriverPath);

            // Launch Exe
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path += PowerToysPath;
            UITestBase.Instance.StartExe("PowerToys", "PowerToys Settings", path);

            var session = UITestBase.Instance.GetCurrentWindow();
            Assert.IsNotNull(session, "Session not initialized");

            // Set implicit timeout to make element search to retry every 500 ms
            session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        }

        public static void Close()
        {
            var session = UITestBase.Instance.GetCurrentWindow();

            // Close the session
            if (session != null)
            {
                session.Quit();
                session.Dispose();
            }

            try
            {
                appDriver?.Kill();
            }
            catch
            {
            }
        }

        // ===================================APPManager API================================================

        // Take control of an application that already exists
        public static void LaunchModuleWithWindowName(PowerToysModuleWindow module)
        {
            UITestBase.Instance.LaunchModuleWithWindowName(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName, ModuleConfigData.Instance.ModuleWindowName[module].WindowName);
        }

        // Use the name to switch the current driver
        public static void SwitchModule(PowerToysModuleWindow module)
        {
            UITestBase.Instance.SwitchApp(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public static void CloseModule(PowerToysModuleWindow module)
        {
            UITestBase.Instance.CloseApp(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public static WindowsDriverWrapper? GetWindowInList(PowerToysModuleWindow module)
        {
            return UITestBase.Instance.GetWindowInList(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public static WindowsDriverWrapper? GetSession(PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            if (module == PowerToysModuleWindow.None)
            {
                return UITestBase.Instance.GetCurrentWindow();
            }
            else
            {
                return UITestBase.Instance.GetWindowInList(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
            }
        }

        public static void TestCode(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            if (session != null)
            {
                Assert.IsNotNull(session, "testSession is null");
                Element? item = session.FindElementByName<Element>(elementName);
                Assert.IsNotNull(item, "ElementName " + elementName + " not found");
            }
        }

        // ===================================Control API================================================
        public static void Enable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = session?.FindElementsByName<Element>("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element.CheckAttribute("HelpText", moduleName))
                    {
                        if (element.CheckAttribute("Toggle.ToggleState", "0"))
                        {
                            element.Click();
                        }

                        buttonFound = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }

        public static void Disable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = session?.FindElementsByName<Element>("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element.CheckAttribute("HelpText", moduleName))
                    {
                        if (element.CheckAttribute("Toggle.ToggleState", "1"))
                        {
                            element.Click();
                        }

                        buttonFound = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }

        public static WindowsElement? OpenContextMenu(string layoutName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var element = session?.FindElementByName<Element>(layoutName);
            element?.RightClick();
            var menu = session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }
    }
}
