// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.UITests.API
{
    public class UITestAPI
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        public WindowsDriver<WindowsElement>? Session { get; private set; }

        public WindowsElement? MainEditorWindow { get; private set; }

        private static Process? appDriver;

        public UITestAPI()
        {
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public void Init(string exePath)
        {
            string winAppDriverPath = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe";
            appDriver = Process.Start(winAppDriverPath);

            // Launch Exe
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path += exePath;

            AppiumOptions opts = new AppiumOptions();
            opts.AddAdditionalCapability("app", path);
            Session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);

            Assert.IsNotNull(Session, "Session not initialized");

            // Set implicit timeout to make element search to retry every 500 ms
            Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);

            // Find main editor window
            try
            {
                MainEditorWindow = Session.FindElementByAccessibilityId("MainWindow1");
            }
            catch
            {
                Assert.IsNotNull(MainEditorWindow, "Main editor window not found");
            }
        }

        public void Close(TestContext testContext)
        {
            // Close the session
            if (Session != null)
            {
                try
                {
                    // FZEditor application can be closed by explicitly closing main editor window
                    MainEditorWindow?.SendKeys(Keys.Alt + Keys.F4);
                }
                catch (Exception ex)
                {
                    testContext.WriteLine(ex.Message);
                }

                Session.Quit();
                Session.Dispose();
            }

            try
            {
                appDriver?.Kill();
            }
            catch
            {
            }
        }

        private WindowsElement? GetElement(string elementName)
        {
            var listItem = Session?.FindElementByName(elementName);
            Assert.IsNotNull(listItem, "ElementName " + elementName + " not found");
            return listItem;
        }

        public WindowsElement? NewOpenContextMenu(string elementName)
        {
            RightClick_Element(elementName);
            var menu = Session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void Click_Element(string elementName)
        {
            var element = GetElement(elementName);
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(30, 30);
            actions.Click();
            actions.Build().Perform();
        }

        public void RightClick_Element(string elementName)
        {
            var element = GetElement(elementName);
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }

        private WindowsElement? GetLayout(string layoutName)
        {
            var listItem = Session?.FindElementByName(layoutName);
            Assert.IsNotNull(listItem, "Layout " + layoutName + " not found");
            return listItem;
        }

        public WindowsElement? OpenContextMenu(string layoutName)
        {
            RightClick_Layout(layoutName);
            var menu = Session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public void Click_CreateNewLayout()
        {
            var button = Session?.FindElementByAccessibilityId("NewLayoutButton");
            Assert.IsNotNull(button, "Create new layout button not found");
            button?.Click();
        }

        public void Click_EditLayout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            var editButton = layout?.FindElementByAccessibilityId("EditLayoutButton");
            Assert.IsNotNull(editButton, "Edit button not found");
            editButton.Click();
        }

        public void RightClick_Layout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            Actions actions = new Actions(Session);
            actions.MoveToElement(layout);
            actions.MoveByOffset(30, 30);
            actions.ContextClick();
            actions.Build().Perform();
        }
    }
}
