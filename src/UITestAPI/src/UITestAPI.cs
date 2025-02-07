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
using static Microsoft.UITests.API.APPManager;
using static Microsoft.UITests.API.ModuleConfigData;

namespace Microsoft.UITests.API
{
    public class UITestAPI
    {
        protected const string PowerToysPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

        private static Process? appDriver;

        public UITestAPI()
        {
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public void Init(string winAppDriverPath = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe")
        {
            appDriver = Process.Start(winAppDriverPath);

            // Launch Exe
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path += PowerToysPath;
            APPManager.Instance.StartExe("PowerToys", "PowerToys Settings", path);

            var session = APPManager.Instance.GetCurrentWindow();
            Assert.IsNotNull(session, "Session not initialized");

            // Set implicit timeout to make element search to retry every 500 ms
            session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        }

        public void Close(TestContext testContext)
        {
            var session = APPManager.Instance.GetCurrentWindow();

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

        // Create a new application and take control of it
        public void StartExe(string appName, string windowName, string appPath)
        {
            APPManager.Instance.StartExe(appName, windowName, appPath);
        }

        // Take control of an application that already exists
        public void LaunchModuleWithWindowName(PowerToysModuleWindow module)
        {
            APPManager.Instance.LaunchModuleWithWindowName(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName, ModuleConfigData.Instance.ModuleWindowName[module].WindowName);
        }

        // Use the name to switch the current driver
        public void SwitchModule(PowerToysModuleWindow module)
        {
            APPManager.Instance.SwitchApp(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public void CloseModule(PowerToysModuleWindow module)
        {
            APPManager.Instance.CloseApp(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public WindowsDriverWrapper? GetWindowInList(PowerToysModuleWindow module)
        {
            return APPManager.Instance.GetWindowInList(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
        }

        public WindowsDriverWrapper? GetSession(PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            if (module == PowerToysModuleWindow.None)
            {
                return APPManager.Instance.GetCurrentWindow();
            }
            else
            {
                return APPManager.Instance.GetWindowInList(ModuleConfigData.Instance.ModuleWindowName[module].ModuleName);
            }
        }

        // ===================================Control API================================================
        private WindowsElement? GetElement(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var item = session?.FindElementByName(elementName);
            Assert.IsNotNull(item, "ElementName " + elementName + " not found");
            return item;
        }

        private ReadOnlyCollection<WindowsElement> GetElements(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var listItem = session?.FindElementsByName(elementName);
            Assert.IsNotNull(listItem, "ElementName " + elementName + " not found");
            return listItem;
        }

        public WindowsElement? NewOpenContextMenu(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            RightClick_Element(elementName);
            var menu = session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void TestCode(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            if (session != null)
            {
                Assert.IsNotNull(session, "testSession is null");
                Element? item = session.FindElementByName<Element>(elementName);
                Assert.IsNotNull(item, "ElementName " + elementName + " not found");
            }
        }

        public void Click_Element(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var element = GetElement(elementName);
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.Click();
            actions.Build().Perform();
        }

        public void Click_Elements(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = GetElements(elementName);
            Actions actions = new Actions(session);
            foreach (var element in elements)
            {
                actions.MoveToElement(element);

                actions.MoveByOffset(5, 5);
                actions.Click();
                actions.Build().Perform();
            }
        }

        public void Click_Element(string elementName, string helpText, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = GetElements(elementName);
            Actions actions = new Actions(session);
            bool buttonClicked = false;
            foreach (var element in elements)
            {
                if (element.GetAttribute("HelpText") == helpText)
                {
                    actions.MoveToElement(element);
                    actions.Click();
                    actions.Build().Perform();
                    actions.MoveByOffset(5, 5);
                    buttonClicked = true;
                    break;
                }
            }

            Assert.IsTrue(buttonClicked, $"No button with elementName '{elementName}' and HelpText '{helpText}' was found.");
        }

        public void Enable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = GetElements("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            foreach (var element in elements)
            {
                if (element.GetAttribute("HelpText") == moduleName)
                {
                    if (element.GetAttribute("Toggle.ToggleState") == "0")
                    {
                        actions.MoveToElement(element);
                        actions.Click();
                        actions.Build().Perform();
                        actions.MoveByOffset(5, 5);
                    }

                    buttonFound = true;
                    break;
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }

        public void Disable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var elements = GetElements("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            foreach (var element in elements)
            {
                if (element.GetAttribute("HelpText") == moduleName)
                {
                    if (element.GetAttribute("Toggle.ToggleState") == "1")
                    {
                        actions.MoveToElement(element);
                        actions.Click();
                        actions.Build().Perform();
                        actions.MoveByOffset(5, 5);
                    }

                    buttonFound = true;
                    break;
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }

        public void RightClick_Element(string elementName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var element = GetElement(elementName);
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }

        private WindowsElement? GetLayout(string layoutName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var listItem = session?.FindElementByName(layoutName);
            Assert.IsNotNull(listItem, "Layout " + layoutName + " not found");
            return listItem;
        }

        public WindowsElement? OpenContextMenu(string layoutName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            RightClick_Layout(layoutName);
            var menu = session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void Click_CreateNewLayout(PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var button = session?.FindElementByAccessibilityId("NewLayoutButton");
            Assert.IsNotNull(button, "Create new layout button not found");
            button?.Click();
        }

        public void Click_EditLayout(string layoutName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            var layout = GetLayout(layoutName, module);
            var editButton = layout?.FindElementByAccessibilityId("EditLayoutButton");
            Assert.IsNotNull(editButton, "Edit button not found");
            editButton.Click();
        }

        public void RightClick_Layout(string layoutName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            WindowsDriverWrapper? session = GetSession(module);
            var layout = GetLayout(layoutName, module);
            Actions actions = new Actions(session);
            actions.MoveToElement(layout);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }
    }
}
