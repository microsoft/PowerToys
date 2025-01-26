// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.UITests.API.APPManager;

namespace Microsoft.UITests.API
{
    public class UITestAPI
    {
        public APPManager APPManager { get; private set; }

        private static Process? appDriver;

        public UITestAPI()
        {
            APPManager = new APPManager();
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public void Init(string appName, string exePath, string windowName, string winAppDriverPath = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe")
        {
            appDriver = Process.Start(winAppDriverPath);

            // Launch Exe
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path += exePath;
            APPManager.StartExe(appName, windowName, path);

            var session = APPManager.GetCurrentWindow();
            Assert.IsNotNull(session, "Session not initialized");

            // Set implicit timeout to make element search to retry every 500 ms
            session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        }

        public void Close(TestContext testContext)
        {
            var session = APPManager.GetCurrentWindow();

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
            APPManager.StartExe(appName, windowName, appPath);
        }

        // Take control of an application that already exists
        public void LaunchModule(string appName, string windowName)
        {
            APPManager.LaunchModule(appName, windowName);
        }

        // Use the name to switch the current driver
        public void SwitchApp(string appName)
        {
            APPManager.SwitchApp(appName);
        }

        public void CloseApp(string appName)
        {
            APPManager.CloseApp(appName);
        }

        public WindowsDriver<WindowsElement>? GetWindowInList(string appName)
        {
            return APPManager.GetWindowInList(appName);
        }

        public WindowsDriver<WindowsElement>? GetSession(string? appName = null)
        {
            if (appName == null)
            {
                return APPManager.GetCurrentWindow();
            }
            else
            {
                return APPManager.GetWindowInList(appName);
            }
        }

        // ===================================Control API================================================
        private WindowsElement? GetElement(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var item = session?.FindElementByName(elementName);
            Assert.IsNotNull(item, "ElementName " + elementName + " not found");
            return item;
        }

        private ReadOnlyCollection<WindowsElement> GetElements(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var listItem = session?.FindElementsByName(elementName);
            Assert.IsNotNull(listItem, "ElementName " + elementName + " not found");
            return listItem;
        }

        public WindowsElement? NewOpenContextMenu(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            RightClick_Element(elementName);
            var menu = session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void Click_Element(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var element = GetElement(elementName);
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.Click();
            actions.Build().Perform();
        }

        public void Click_Elements(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
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

        public void Click_Element(string elementName, string helpText, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
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

        public void Enable_Module_from_Dashboard(string moduleName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
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

        public void Disable_Module_from_Dashboard(string moduleName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
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

        public void RightClick_Element(string elementName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var element = GetElement(elementName);
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }

        private WindowsElement? GetLayout(string layoutName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var listItem = session?.FindElementByName(layoutName);
            Assert.IsNotNull(listItem, "Layout " + layoutName + " not found");
            return listItem;
        }

        public WindowsElement? OpenContextMenu(string layoutName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            RightClick_Layout(layoutName);
            var menu = session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void Click_CreateNewLayout(string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var button = session?.FindElementByAccessibilityId("NewLayoutButton");
            Assert.IsNotNull(button, "Create new layout button not found");
            button?.Click();
        }

        public void Click_EditLayout(string layoutName, string? appName = null)
        {
            var layout = GetLayout(layoutName, appName);
            var editButton = layout?.FindElementByAccessibilityId("EditLayoutButton");
            Assert.IsNotNull(editButton, "Edit button not found");
            editButton.Click();
        }

        public void RightClick_Layout(string layoutName, string? appName = null)
        {
            WindowsDriver<WindowsElement>? session = GetSession(appName);
            var layout = GetLayout(layoutName, appName);
            Actions actions = new Actions(session);
            actions.MoveToElement(layout);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }
    }
}
