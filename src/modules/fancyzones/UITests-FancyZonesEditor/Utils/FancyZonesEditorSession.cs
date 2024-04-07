// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.FancyZonesEditor.UnitTests.Utils
{
    public class FancyZonesEditorSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string FancyZonesEditorPath = @"\..\..\..\PowerToys.FancyZonesEditor.exe";

        private static FancyZonesEditorFiles? _files;

        public static FancyZonesEditorFiles Files
        {
            get
            {
                if (_files == null)
                {
                    _files = new FancyZonesEditorFiles();
                }

                return _files;
            }
        }

        public WindowsDriver<WindowsElement>? Session { get; }

        public WindowsElement? MainEditorWindow { get; }

        public FancyZonesEditorSession(TestContext testContext)
        {
            try
            {
                // Launch FancyZonesEditor
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += FancyZonesEditorPath;

                AppiumOptions opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", path);
                Session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.Message);
            }

            Assert.IsNotNull(Session, "Session not initialized");

            testContext.WriteLine("Session: " + Session.SessionId.ToString());
            testContext.WriteLine("Title: " + Session.Title);

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
