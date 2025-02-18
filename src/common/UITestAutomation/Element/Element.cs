// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;
using static Microsoft.PowerToys.UITest.UITestBase;

[assembly: InternalsVisibleTo("Session")]

namespace Microsoft.PowerToys.UITest
{
    // Represents a basic UI element in the application.
    public class Element
    {
        // WindowsElement and WindowsDriver are components of WinAppDriver that provide underlying element operations.
        private WindowsElement? WindowsElement { get; set; }

        private WindowsDriver<WindowsElement>? driver;

        // The name of the UI element.
        public string Name
        {
            get { return GetAttribute("Name"); }
        }

        // The text of the UI element.
        public string Test
        {
            get { return GetAttribute("Value"); }
        }

        // The automation ID of the UI element.
        public string AutomationId
        {
            get { return GetAttribute("AutomationId"); }
        }

        // The class name of the UI element.
        public string ClassName
        {
            get { return GetAttribute("ClassName"); }
        }

        // The help text of the UI element.
        public string HelpText
        {
            get { return GetAttribute("HelpText"); }
        }

        internal void SetWindowsElement(WindowsElement windowsElement) => this.WindowsElement = windowsElement;

        internal void SetSession(WindowsDriver<WindowsElement> driver) => this.driver = driver;

        public Element()
        {
            WindowsElement = null;
        }

        // Checks if the UI element is enabled.
        // Returns:
        //   bool: True if the element is enabled; otherwise, false.
        public bool IsEnabled() => GetAttribute("IsEnabled") == "True";

        // Checks if the UI element is selected.
        // Returns:
        //   bool: True if the element is selected; otherwise, false.
        public bool IsSelected() => GetAttribute("IsSelected") == "True";

        // Click the UI element
        public void LeftClick() => PerformAction(actions => actions.Click());

        // Right click the UI element
        public void RightClick() => PerformAction(actions => actions.ContextClick());

        // Underlying function to get attribute by WindowsElement
        public string GetAttribute(string attributeName)
        {
            Assert.IsNotNull(this.WindowsElement, "WindowsElement should not be null");
            return this.WindowsElement?.GetAttribute(attributeName) ?? string.Empty;
        }

        public T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsElement, "WindowsElement is null");
            return FindElementHelper.Find<T, AppiumWebElement>(() => this.WindowsElement.FindElement(by.ToSeleniumBy()), timeoutMS, this.driver);
        }

        // Find elements by name
        public ReadOnlyCollection<T>? FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindAll<T, AppiumWebElement>(() => this.WindowsElement.FindElements(by.ToSeleniumBy()), timeoutMS, this.driver);
        }

        // Simulate manual operation
        private void PerformAction(Action<Actions> action)
        {
            var element = this.WindowsElement;
            Actions actions = new Actions(this.driver);
            actions.MoveToElement(element);
            action(actions);
            actions.Build().Perform();
        }
    }
}
