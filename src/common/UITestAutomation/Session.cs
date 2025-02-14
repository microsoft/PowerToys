// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    // Class representing a session for UI testing
    public class Session
    {
        // Property to hold the root driver
        private WindowsDriver<WindowsElement> Root { get; set; }

        // Property to hold the Windows driver
        private WindowsDriver<WindowsElement> WindowsDriver { get; set; }

        // Importing user32.dll to set the foreground window
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        // Constructor to initialize the session with root and Windows driver
        public Session(WindowsDriver<WindowsElement> root, WindowsDriver<WindowsElement> windowsDriver)
        {
            Root = root;
            WindowsDriver = windowsDriver;
        }

        // Method to find an element by a given selector
        public T FindElement<T>(By by)
            where T : Element, new()
        {
            var item = WindowsDriver.FindElement(by.ToSeleniumBy());
            Assert.IsNotNull(item, "Can't find this element");
            return NewElement<T>(item);
        }

        // Method to find an element by its name
        public T FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = WindowsDriver.FindElementByName(name);
            Assert.IsNotNull(item, "Can't find this element");
            return NewElement<T>(item);
        }

        // Method to find multiple elements by their name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name)
            where T : Element, new()
        {
            var items = WindowsDriver.FindElementsByName(name);
            Assert.IsNotNull(items, "Can't find this element");
            var res = items.Select(NewElement<T>).ToList();
            return new ReadOnlyCollection<T>(res);
        }

        // Method to create a new element of type T
        private T NewElement<T>(WindowsElement element)
            where T : Element, new()
        {
            T newElement = new T();
            newElement.SetSession(WindowsDriver);
            newElement.SetWindowsElement(element);
            return newElement;
        }

        // Method to take control of an existing application
        public Session? Attach(PowerToysModuleWindow module)
        {
            Thread.Sleep(4000);
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
            }
            else
            {
                Assert.IsNotNull(Root, "Root driver is null");
            }

            return null;
        }
    }
}
