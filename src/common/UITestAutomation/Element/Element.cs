﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

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

        protected string? TargetControlType { get; set; }

        internal bool IsMatchingTarget()
        {
            var ct = this.ControlType;
            return string.IsNullOrEmpty(this.TargetControlType) || this.TargetControlType == this.ControlType;
        }

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
            get { return this.windowsElement?.Text ?? string.Empty; }
        }

        /// <summary>
        /// Gets a value indicating whether the UI element is Enabled or not.
        /// </summary>
        public bool Enabled
        {
            get { return this.windowsElement?.Enabled ?? false; }
        }

        public bool Selected
        {
            get { return this.windowsElement?.Selected ?? false; }
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
        /// Click the UI element.
        /// </summary>
        /// <param name="rightClick">If true, performs a right-click; otherwise, performs a left-click. Default value is false</param>
        public virtual void Click(bool rightClick = false)
        {
            PerformAction((actions, windowElement) =>
            {
                actions.MoveToElement(windowElement);

                // Move 2by2 offset to make click more stable instead of click on the border of the element
                actions.MoveByOffset(2, 2);

                if (rightClick)
                {
                    actions.ContextClick();
                }
                else
                {
                    actions.Click();
                }

                actions.Build().Perform();
            });
        }

        /// <summary>
        /// Double Click the UI element.
        /// </summary>
        public virtual void DoubleClick()
        {
            PerformAction((actions, windowElement) =>
            {
                actions.MoveToElement(windowElement);

                // Move 2by2 offset to make click more stable instead of click on the border of the element
                actions.MoveByOffset(2, 2);

                actions.DoubleClick();
                actions.Build().Perform();
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

            // leverage findAll to filter out mismatched elements
            var collection = this.FindAll<T>(by, timeoutMS);

            Assert.IsTrue(collection.Count > 0, $"Element not found using selector: {by}");

            return collection[0];
        }

        /// <summary>
        /// Finds an element by the selector.
        /// Shortcut for this.Find<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class type of the element to find.</typeparam>
        /// <param name="name">The name for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>The found element.</returns>
        public T Find<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Find<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds an element by the selector.
        /// Shortcut for this.Find<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to use for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>The found element.</returns>
        public Element Find(By by, int timeoutMS = 3000)
        {
            return this.Find<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Finds an element by the selector.
        /// Shortcut for this.Find<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>The found element.</returns>
        public Element Find(string name, int timeoutMS = 3000)
        {
            return this.Find<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds all elements by the selector.
        /// </summary>
        /// <typeparam name="T">The class type of the elements to find.</typeparam>
        /// <param name="by">The selector to use for finding the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindHelper.FindAll<T, AppiumWebElement>(
                () =>
                {
                    var elements = this.windowsElement.FindElements(by.ToSeleniumBy());
                    return elements;
                },
                this.driver,
                timeoutMS);

            return foundElements ?? new ReadOnlyCollection<T>([]);
        }

        /// <summary>
        /// Finds all elements by the selector.
        /// Shortcut for this.FindAll<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class type of the elements to find.</typeparam>
        /// <param name="name">The name for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.FindAll<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds all elements by the selector.
        /// Shortcut for this.FindAll<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to use for finding the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 3000)
        {
            return this.FindAll<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Finds all elements by the selector.
        /// Shortcut for this.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name for finding the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds.</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 3000)
        {
            return this.FindAll<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Simulates a manual operation on the element.
        /// </summary>
        /// <param name="action">The action to perform on the element.</param>
        /// <param name="msPreAction">The number of milliseconds to wait before the action. Default value is 500 ms</param>
        /// <param name="msPostAction">The number of milliseconds to wait after the action. Default value is 500 ms</param>
        protected void PerformAction(Action<Actions, WindowsElement> action, int msPreAction = 500, int msPostAction = 500)
        {
            if (msPreAction > 0)
            {
                Task.Delay(msPreAction).Wait();
            }

            var windowElement = this.windowsElement!;
            Actions actions = new Actions(this.driver);
            action(actions, windowElement);

            if (msPostAction > 0)
            {
                Task.Delay(msPostAction).Wait();
            }
        }
    }
}
