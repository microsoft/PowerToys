// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides interfaces for interacting with UI elements.
    /// </summary>
    public class Session
    {
        private WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement> WindowsDriver { get; set; }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        public Session(WindowsDriver<WindowsElement> root, WindowsDriver<WindowsElement> windowsDriver)
        {
            this.Root = root;
            this.WindowsDriver = windowsDriver;
        }

        /// <summary>
        /// Finds an element by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        public T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsDriver, $"WindowsElement is null in method Find<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");

            // leverage findAll to filter out mismatched elements
            var collection = this.FindAll<T>(by, timeoutMS);

            Assert.IsTrue(collection.Count > 0, $"Element not found using selector: {by}");

            return collection[0];
        }

        /// <summary>
        /// Shortcut for this.Find<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        public T Find<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Find<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        public Element Find(By by, int timeoutMS = 3000)
        {
            return this.Find<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        public Element Find(string name, int timeoutMS = 3000)
        {
            return this.Find<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsDriver, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindHelper.FindAll<T, WindowsElement>(
                () =>
                {
                    if (by.GetIsAccessibilityId())
                    {
                        var elements = this.WindowsDriver.FindElementsByAccessibilityId(by.GetAccessibilityId());
                        return elements;
                    }
                    else
                    {
                        var elements = this.WindowsDriver.FindElements(by.ToSeleniumBy());
                        return elements;
                    }
                },
                this.WindowsDriver,
                timeoutMS);

            return foundElements ?? new ReadOnlyCollection<T>([]);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="name">The name to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.FindAll<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 3000)
        {
            return this.FindAll<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 3000)
        {
            return this.FindAll<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Keyboard Action key.
        /// </summary>
        /// <param name="key1">The Keys1 to click.</param>
        /// <param name="key2">The Keys2 to click.</param>
        /// <param name="key3">The Keys3 to click.</param>
        /// <param name="key4">The Keys4 to click.</param>
        public void KeyboardAction(string key1, string key2 = "", string key3 = "", string key4 = "")
        {
            PerformAction((actions, windowElement) =>
            {
                if (string.IsNullOrEmpty(key2))
                {
                    actions.SendKeys(key1);
                }
                else if (string.IsNullOrEmpty(key3))
                {
                    actions.SendKeys(key1).SendKeys(key2);
                }
                else if (string.IsNullOrEmpty(key4))
                {
                    actions.SendKeys(key1).SendKeys(key2).SendKeys(key3);
                }
                else
                {
                    actions.SendKeys(key1).SendKeys(key2).SendKeys(key3).SendKeys(key4);
                }

                actions.Release();
                actions.Build().Perform();
            });
        }

        /// <summary>
        /// Attaches to an existing PowerToys module.
        /// </summary>
        /// <param name="module">The PowerToys module to attach to.</param>
        /// <returns>The attached session.</returns>
        public Session Attach(PowerToysModule module)
        {
            string windowName = ModuleConfigData.Instance.GetModuleWindowName(module);
            return this.Attach(windowName);
        }

        /// <summary>
        /// Attaches to an existing exe by string window name.
        /// The session should be attached when a new app is started.
        /// </summary>
        /// <param name="windowName">The window name to attach to.</param>
        /// <returns>The attached session.</returns>
        public Session Attach(string windowName)
        {
            if (this.Root != null)
            {
                var window = this.Root.FindElementByName(windowName);
                Assert.IsNotNull(window, $"Failed to attach. Window '{windowName}' not found");

                var windowHandle = new nint(int.Parse(window.GetAttribute("NativeWindowHandle")));
                SetForegroundWindow(windowHandle);
                var hexWindowHandle = windowHandle.ToString("x");
                var appCapabilities = new AppiumOptions();

                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                this.WindowsDriver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), appCapabilities);
                Assert.IsNotNull(this.WindowsDriver, "Attach WindowsDriver is null");

                // Set implicit timeout to make element search retry every 500 ms
                this.WindowsDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            }
            else
            {
                Assert.IsNotNull(this.Root, $"Failed to attach to the window '{windowName}'. Root driver is null");
            }

            return this;
        }

        /// <summary>
        /// Simulates a manual operation on the element.
        /// </summary>
        /// <param name="action">The action to perform on the element.</param>
        /// <param name="msPreAction">The number of milliseconds to wait before the action. Default value is 500 ms</param>
        /// <param name="msPostAction">The number of milliseconds to wait after the action. Default value is 500 ms</param>
        protected void PerformAction(Action<Actions, WindowsDriver<WindowsElement>> action, int msPreAction = 500, int msPostAction = 500)
        {
            if (msPreAction > 0)
            {
                Task.Delay(msPreAction).Wait();
            }

            var windowsDriver = this.WindowsDriver;
            Actions actions = new Actions(this.WindowsDriver);
            action(actions, windowsDriver);

            if (msPostAction > 0)
            {
                Task.Delay(msPostAction).Wait();
            }
        }
    }
}
