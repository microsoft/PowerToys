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
    // Class representing a UI element in the application
    public class Element
    {
        // Property to hold the Windows element
        public WindowsElement? WindowsElement { get; set; }

        // Property to hold the Windows driver
        private WindowsDriver<WindowsElement>? driver;

        // Constructor to initialize the element
        public Element() => WindowsElement = null;

        // Method to set the Windows element
        internal void SetWindowsElement(WindowsElement windowsElement) => WindowsElement = windowsElement;

        // Method to set the session driver
        internal void SetSession(WindowsDriver<WindowsElement> driver) => this.driver = driver;

        // Method to get the name attribute of the element
        public string GetName() => GetAttribute("Name");

        // Method to get the text attribute of the element
        public string GetText() => GetAttribute("Value");

        // Method to get the automation ID of the element
        public string GetAutomationId() => GetAttribute("AutomationId");

        // Method to get the class name of the element
        public string GetClassName() => GetAttribute("ClassName");

        // Method to get the help text of the element
        public string GetHelpText() => GetAttribute("HelpText");

        // Method to check if the element is enabled
        public bool IsEnable() => GetAttribute("IsEnabled") == "True";

        // Method to check if the element is selected
        public bool IsSelected() => GetAttribute("IsSelected") == "True";

        // Method to click the element
        public void Click() => PerformAction(actions => actions.Click());

        // Method to right-click the element
        public void RightClick() => PerformAction(actions => actions.ContextClick());

        // Method to check if a specific attribute matches a value
        public bool CheckAttribute(string attributeKey, string attributeValue) => GetAttribute(attributeKey) == attributeValue;

        // Method to find an element by its name
        public T FindElementByName<T>(string name, int timeoutInMilliseconds = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElement<T, AppiumWebElement>(() => WindowsElement.FindElementByName(name), timeoutInMilliseconds, driver);
        }

        // Method to find an element by its accessibility ID
        public T? FindElementByAccessibilityId<T>(string name, int timeoutInMilliseconds = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElement<T, AppiumWebElement>(() => WindowsElement.FindElementByAccessibilityId(name), timeoutInMilliseconds, driver);
        }

        // Method to find multiple elements by their name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name, int timeoutInMilliseconds = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement is null");
            return FindElementHelper.FindElements<T, AppiumWebElement>(() => WindowsElement.FindElementsByName(name), timeoutInMilliseconds, driver);
        }

        // Method to take a screenshot of the element
        public Screenshot? GetScreenShot() => WindowsElement?.GetScreenshot();

        // Helper method to get an attribute of the element
        private string GetAttribute(string attributeName)
        {
            Assert.IsNotNull(WindowsElement, $"{attributeName} should not be null");
            return WindowsElement?.GetAttribute(attributeName) ?? string.Empty;
        }

        // Helper method to perform an action on the element
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
