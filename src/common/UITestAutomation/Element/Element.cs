// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;
using static Microsoft.PowerToys.UITest.UITestBase;

[assembly: InternalsVisibleTo("Session")]

namespace Microsoft.PowerToys.UITest
{
    // The basic class for all UI elements
    public class Element
    {
        public WindowsElement? WindowsElement { get; set; }

        private WindowsDriver<WindowsElement>? driver;

        public Element() => WindowsElement = null;

        internal void SetWindowsElement(WindowsElement windowsElement) => WindowsElement = windowsElement;

        internal void SetSession(WindowsDriver<WindowsElement> driver) => this.driver = driver;

        // Get the name of the element
        public string GetName() => GetAttribute("Name");

        // Get the text of the element
        public string GetText() => GetAttribute("Value");

        // Get the automation ID of the element
        public string GetAutomationId() => GetAttribute("AutomationId");

        // Get the class name of the element
        public string GetClassName() => GetAttribute("ClassName");

        // Get the help text of the element
        public string GetHelpText() => GetAttribute("HelpText");

        // Check if the element is enabled
        public bool IsEnabled() => GetAttribute("IsEnabled") == "True";

        // Check if the element is selected
        public bool IsSelected() => GetAttribute("IsSelected") == "True";

        // Click the element
        public void Click() => PerformAction(actions => actions.Click());

        // Right click the element
        public void RightClick() => PerformAction(actions => actions.ContextClick());

        // Get an attribute of the element
        private string GetAttribute(string attributeName)
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            return WindowsElement?.GetAttribute(attributeName) ?? string.Empty;
        }

        // Find element by Name
        public T FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByName(name) as WindowsElement;
            Assert.IsNotNull(item, "Can't find this element");
            return NewElement<T>(item);
        }

        // Method element by AccessibilityId
        public T? FindElementByAccessibilityId<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByAccessibilityId(name) as WindowsElement;
            Assert.IsNotNull(item, "Can't find this element");
            return NewElement<T>(item);
        }

        // Find elements by name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name)
            where T : Element, new()
        {
            var items = WindowsElement?.FindElementsByName(name);
            Assert.IsNotNull(items, "Can't find this element");
            var res = items.Select(item =>
            {
                var element = item as WindowsElement;
                if (element != null)
                {
                    NewElement<T>(element);
                }

                return element;
            }).ToList();
            return res == null ? null : new ReadOnlyCollection<T>((IList<T>)res);
        }

        public Screenshot? GetScreenShot() => WindowsElement?.GetScreenshot();

        // Simulate manual operation
        private void PerformAction(Action<Actions> action)
        {
            var element = WindowsElement;
            Actions actions = new Actions(driver);
            actions.MoveToElement(element);
            action(actions);
            actions.Build().Perform();
        }

        private T NewElement<T>(WindowsElement element)
             where T : Element, new()
        {
            T newElement = new T();
            Assert.IsNotNull(driver, "[Element.cs] driver is null");
            newElement.SetSession(driver);
            Assert.IsNotNull(element, "[Element.cs] element is null");
            newElement.SetWindowsElement(element);
            return newElement;
        }
    }
}
