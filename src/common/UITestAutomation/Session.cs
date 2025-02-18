// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    // Wrap WinAppDriver and provide interfaces to users
    public class Session
    {
        private WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement> WindowsDriver { get; set; }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        // Initializes a new instance of the Session class.
        // Parameters:
        //   WindowsDriver<WindowsElement> root: The root WindowsDriver for the desktop.
        //   WindowsDriver<WindowsElement> windowsDriver: The WindowsDriver for the application.
        public Session(WindowsDriver<WindowsElement> root, WindowsDriver<WindowsElement> windowsDriver)
        {
            Root = root;
            WindowsDriver = windowsDriver;
        }

        // Finds an element by selector.
        // Type parameters:
        //   T: The class of the element, should be Element or its derived class.
        // Parameters:
        //   By by: The selector to find the element.
        //   int timeoutMS: The timeout in milliseconds (default is 3000).
        // Returns:
        //   T: The found element.
        public T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsDriver, "WindowsElement is null");
            return FindElementHelper.Find<T, WindowsElement>(() => WindowsDriver.FindElement(by.ToSeleniumBy()), timeoutMS, WindowsDriver);
        }

        // Find elements by name
        public ReadOnlyCollection<T>? FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsDriver, "WindowsElement is null");
            return FindElementHelper.FindAll<T, WindowsElement>(() => WindowsDriver.FindElements(by.ToSeleniumBy()), timeoutMS, WindowsDriver);
        }

        public Session Attach(PowerToysModuleWindow module)
        {
            string windowName = ModuleConfigData.Instance.GetModuleWindowName(module);
            return AttachByWindowName(windowName);
        }

        // Attaches to an existing exe by window name.
        // The session should be attached when a new app is started. e.g. launching KeyboardmanagerEditor from settings.
        // Parameters:
        //   PowerToysModuleWindow module: The module window to attach to.
        // Returns:
        //   Session: The attached session.
        public Session AttachByWindowName(string windowName)
        {
            if (Root != null)
            {
                var window = Root.FindElementByName(windowName);
                Assert.IsNotNull(window, $"{windowName} not found");

                var windowHandle = new nint(int.Parse(window.GetAttribute("NativeWindowHandle")));
                SetForegroundWindow(windowHandle);
                var hexWindowHandle = windowHandle.ToString("x");
                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                WindowsDriver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), appCapabilities);
                Assert.IsNotNull(WindowsDriver, "Attach WindowsDriver is null");

                // Set implicit timeout to make element search retry every 500 ms
                WindowsDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            }
            else
            {
                Assert.IsNotNull(Root, "Root driver is null");
            }

            return this;
        }
    }
}
