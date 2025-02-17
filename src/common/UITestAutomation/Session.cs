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

        public Session(WindowsDriver<WindowsElement> root, WindowsDriver<WindowsElement> windowsDriver)
        {
            Root = root;
            WindowsDriver = windowsDriver;
        }

        // Find element by selector
        public T FindElement<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsDriver, "WindowsElement is null");
            return FindElementHelper.FindElement<T, WindowsElement>(() => WindowsDriver.FindElement(by.ToSeleniumBy()), timeoutMS, WindowsDriver);
        }

        // Find element by name
        public T FindElementByName<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsDriver, "WindowsElement is null");
            return FindElementHelper.FindElement<T, WindowsElement>(() => WindowsDriver.FindElementByName(name), timeoutMS, WindowsDriver);
        }

        // ind elements by name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsDriver, "WindowsElement is null");
            return FindElementHelper.FindElements<T, WindowsElement>(() => WindowsDriver.FindElementsByName(name), timeoutMS, WindowsDriver);
        }

        // Attach to an existing exe by window name
        public Session? Attach(PowerToysModuleWindow module)
        {
            string windowName = ModuleConfigData.Instance.GetModuleWindowData(module).WindowName;

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

            return null;
        }
    }
}
