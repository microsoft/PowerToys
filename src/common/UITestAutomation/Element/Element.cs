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
        public WindowsElement? WindowsElement { get; set; }

        private WindowsDriver<WindowsElement>? driver;

        public Element() => WindowsElement = null;

        internal void SetWindowsElement(WindowsElement windowsElement) => WindowsElement = windowsElement;

        internal void SetSession(WindowsDriver<WindowsElement> driver) => this.driver = driver;

        // Gets the name of the UI element.
        // Returns:
        //   string: The Name attribute of the element.
        public string GetName() => GetAttribute("Name");

        // Gets the text of the UI element.
        // Returns:
        //   string: The Value attribute of the element.
        public string GetText() => GetAttribute("Value");

        // Gets the automation ID of the UI element.
        // Returns:
        //   string: The AutomationID attribute of the element.
        public string GetAutomationId() => GetAttribute("AutomationId");

        // Gets the class name of the UI element.
        // Returns:
        //   string: The ClassName attribute of the element.
        public string GetClassName() => GetAttribute("ClassName");

        // Gets the help text of the UI element.
        // Returns:
        //   string: The HelpText attribute of the element.
        public string GetHelpText() => GetAttribute("HelpText");

        // Checks if the UI element is enabled.
        // Returns:
        //   bool: True if the element is enabled; otherwise, false.
        public bool IsEnabled() => GetAttribute("IsEnabled") == "True";

        // Checks if the UI element is selected.
        // Returns:
        //   bool: True if the element is selected; otherwise, false.
        public bool IsSelected() => GetAttribute("IsSelected") == "True";

        // Click the UI element
        public void Click() => PerformAction(actions => actions.Click());

        // Right click the UI element
        public void RightClick() => PerformAction(actions => actions.ContextClick());

        // Underlying function to get attribute by WindowsElement
        private string GetAttribute(string attributeName)
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            return WindowsElement?.GetAttribute(attributeName) ?? string.Empty;
        }

        // Find element by Name
        public T FindElementByName<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElement<T, AppiumWebElement>(() => WindowsElement.FindElementByName(name), timeoutMS, driver);
        }

        // Find element by AccessibilityId
        public T? FindElementByAccessibilityId<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElement<T, AppiumWebElement>(() => WindowsElement.FindElementByAccessibilityId(name), timeoutMS, driver);
        }

        // Find elements by name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElements<T, AppiumWebElement>(() => WindowsElement.FindElementsByName(name), timeoutMS, driver);
        }

        // Simulate manual operation
        private void PerformAction(Action<Actions> action)
        {
            var element = WindowsElement;
            Actions actions = new Actions(driver);
            actions.MoveToElement(element);
            action(actions);
            actions.Build().Perform();
        }
    }
}
