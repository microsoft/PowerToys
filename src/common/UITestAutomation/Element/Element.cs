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
    /// <summary>
    /// Represents a basic UI element in the application.
    /// </summary>
    public class Element
    {
        private WindowsElement? windowsElement;
        private WindowsDriver<WindowsElement>? driver;

        internal void SetWindowsElement(WindowsElement windowsElement) => this.windowsElement = windowsElement;

        internal void SetSession(WindowsDriver<WindowsElement> driver) => this.driver = driver;

        /// <summary>
        /// Gets the name of the UI element.
        /// </summary>
        public string Name
        {
            get { return GetAttribute("Name"); }
        }

        /// <summary>
        /// Gets the text of the UI element.
        /// </summary>
        public string Text
        {
            get { return GetAttribute("Value"); }
        }

        /// <summary>
        /// Gets the AutomationID of the UI element.
        /// </summary>
        public string AutomationId
        {
            get { return GetAttribute("AutomationId"); }
        }

        /// <summary>
        /// Gets the class name of the UI element.
        /// </summary>
        public string ClassName
        {
            get { return GetAttribute("ClassName"); }
        }

        /// <summary>
        /// Gets the help text of the UI element.
        /// </summary>
        public string HelpText
        {
            get { return GetAttribute("HelpText"); }
        }

        /// <summary>
        /// Gets the control type of the UI element.
        /// </summary>
        public string ControlType
        {
            get { return GetAttribute("ControlType"); }
        }

        /// <summary>
        /// Checks if the UI element is enabled.
        /// </summary>
        /// <returns>True if the element is enabled; otherwise, false.</returns>
        public bool IsEnabled() => GetAttribute("IsEnabled") == "True";

        /// <summary>
        /// Checks if the UI element is selected.
        /// </summary>
        /// <returns>True if the element is selected; otherwise, false.</returns>
        public bool IsSelected() => GetAttribute("IsSelected") == "True";

        /// <summary>
        /// Click the UI element.
        /// </summary>
        /// <param name="rightClick">If true, performs a right-click; otherwise, performs a left-click.</param>
        public void Click(bool rightClick = false)
        {
            PerformAction(actions =>
            {
                if (rightClick)
                {
                    actions.ContextClick();
                }
                else
                {
                    actions.Click();
                }
            });
        }

        /// <summary>
        /// Gets the attribute value of the UI element.
        /// </summary>
        /// <param name="attributeName">The name of the attribute to get.</param>
        /// <returns>The value of the attribute.</returns>
        public string GetAttribute(string attributeName)
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method GetAttribute with parameter: attributeName = {attributeName}");
            var attributeValue = this.windowsElement.GetAttribute(attributeName);
            Assert.IsNotNull(attributeValue, $"Attribute '{attributeName}' is null.");
            return attributeValue;
        }

        /// <summary>
        /// Finds an element by the selector.
        /// </summary>
        /// <typeparam name="T">The class type of the element to find.</typeparam>
        /// <param name="by">The selector to use for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>The found element.</returns>
        public T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method Find<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElement = FindElementHelper.Find<T, AppiumWebElement>(
                () =>
                {
                    var element = this.windowsElement.FindElement(by.ToSeleniumBy());
                    Assert.IsNotNull(element, $"Element not found using selector: {by}");
                    return element;
                },
                this.driver,
                timeoutMS);

            return foundElement;
        }

        /// <summary>
        /// Finds all elements by the selector.
        /// </summary>
        /// <typeparam name="T">The class type of the elements to find.</typeparam>
        /// <param name="by">The selector to use for finding the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T>? FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindElementHelper.FindAll<T, AppiumWebElement>(
                () =>
                {
                    var elements = this.windowsElement.FindElements(by.ToSeleniumBy());
                    Assert.IsTrue(elements.Count > 0, $"Elements not found using selector: {by}");
                    return elements;
                },
                this.driver,
                timeoutMS);

            return foundElements;
        }

        /// <summary>
        /// Simulates a manual operation on the element.
        /// </summary>
        private void PerformAction(Action<Actions> action)
        {
            var element = this.windowsElement;
            Actions actions = new Actions(this.driver);
            actions.MoveToElement(element);
            action(actions);
            actions.Build().Perform();
        }
    }
}
