// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;
using static Microsoft.PowerToys.UITest.UITestBase;

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
        public Element()
        {
            WindowsElement = null;
        }

        // Method to set the Windows element
        public void SetWindowsElement(WindowsElement windowsElement)
        {
            WindowsElement = windowsElement;
        }

        // Method to set the session driver
        public void SetSession(WindowsDriver<WindowsElement> driver)
        {
            this.driver = driver;
        }

        // Method to get the name attribute of the element
        public string GetName()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("Name");
        }

        // Method to get the text attribute of the element
        public string GetText()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("Value");
        }

        // Method to get the automation ID of the element
        public string GetAutomationId()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("AutomationId");
        }

        // Method to get the class name of the element
        public string GetClassName()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("ClassName");
        }

        // Method to get the help text of the element
        public string GetHelpText()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("HelpText");
        }

        // Method to check if the element is enabled
        public bool IsEnable()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
            }

            return WindowsElement?.GetAttribute("IsEnabled") == "True" ? true : false;
        }

        // Method to check if the element is selected
        public bool IsSelected()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
            }

            return WindowsElement?.GetAttribute("IsSelected") == "True" ? true : false;
        }

        // Method to click the element
        public void Click()
        {
            var element = WindowsElement;
            Actions actions = new Actions(driver);
            actions.MoveToElement(element);
            actions.Click();
            actions.Build().Perform();
        }

        // Method to right-click the element
        public void RightClick()
        {
            var element = WindowsElement;
            Actions actions = new Actions(driver);
            actions.MoveToElement(element);
            actions.MoveByOffset(5, 5);
            actions.ContextClick();
            actions.Build().Perform();
        }

        // Method to click the element if a specific attribute matches a value
        public void ClickCheckAttribute(string attributeKey, string attributeValue)
        {
            var elements = WindowsElement;
            Actions actions = new Actions(driver);
            if (elements?.GetAttribute(attributeKey) == attributeValue)
            {
                actions.MoveToElement(elements);
                actions.Click();
                actions.Build().Perform();
                actions.MoveByOffset(5, 5);
            }
        }

        // Method to check if a specific attribute matches a value
        public bool CheckAttribute(string attributeKey, string attributeValue)
        {
            var elements = WindowsElement;
            return elements?.GetAttribute(attributeKey) == attributeValue;
        }

        // Method to find an element by its name
        public T FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByName(name) as WindowsElement;
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

        // Method to find an element by its accessibility ID
        public T? FindElementByAccessibilityId<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByAccessibilityId(name) as WindowsElement;
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

        // Method to find multiple elements by their name
        public ReadOnlyCollection<T>? FindElementsByName<T>(string name)
            where T : Element, new()
        {
            var items = WindowsElement?.FindElementsByName(name);
            Assert.IsNotNull(items, "Can`t find this element");
            List<T> res = new List<T>();
            foreach (var item in items)
            {
                T element = new T();
                var itemTemp = item as WindowsElement;
                if (itemTemp != null)
                {
                    element.SetWindowsElement(itemTemp);
                }

                res.Add(element);
            }

            var resReadOnlyCollection = new ReadOnlyCollection<T>(res);
            return resReadOnlyCollection;
        }

        // Method to take a screenshot of the element
        public Screenshot? GetScreenShot()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return null;
            }

            return WindowsElement?.GetScreenshot();
        }
    }
}
