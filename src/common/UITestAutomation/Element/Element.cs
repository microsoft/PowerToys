// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ABI.Windows.Foundation;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
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

        protected internal WindowsElement? WindowsElement
        {
            get => windowsElement;
            set => windowsElement = value;
        }

        private WindowsDriver<WindowsElement>? driver;

        protected internal WindowsDriver<WindowsElement>? Driver
        {
            get => driver;
            set => driver = value;
        }

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
        /// Gets a value indicating whether the UI element is visible to the user.
        /// </summary>
        public bool Displayed
        {
            get { return this.windowsElement?.Displayed ?? false; }
        }

        /// <summary>
        /// Gets the Rect of the UI element.
        /// </summary>
        public Rectangle? Rect
        {
            get { return this.windowsElement?.Rect; }
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
        /// <param name="msPreAction">Delay in milliseconds before performing the click action. Default is 500 ms.</param>
        /// <param name="msPostAction">Delay in milliseconds after performing the click action. Default is 500 ms.</param>
        public virtual void Click(bool rightClick = false, int msPreAction = 500, int msPostAction = 500)
        {
            PerformAction(
                (actions, windowElement) =>
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
            },
                msPreAction,
                msPostAction);
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
        /// Release action
        /// </summary>
        public void ReleaseAction()
        {
            var releaseAction = new Actions(driver);
            releaseAction.Release().Perform();
        }

        /// <summary>
        /// Release key
        /// </summary>
        public void ReleaseKey(Key key)
        {
            KeyboardHelper.ReleaseKey(key);
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
        public T Find<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method Find<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");

            // leverage findAll to filter out mismatched elements
            var collection = this.FindAll<T>(by, timeoutMS);

            Assert.IsTrue(collection.Count > 0, $"UI-Element({typeof(T).Name}) not found using selector: {by}");

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
        public T Find<T>(string name, int timeoutMS = 5000)
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
        public Element Find(By by, int timeoutMS = 5000)
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
        public Element Find(string name, int timeoutMS = 5000)
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
        public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindHelper.FindAll<T, AppiumWebElement>(
                () =>
                {
                    if (by.GetIsAccessibilityId())
                    {
                        var elements = this.windowsElement.FindElementsByAccessibilityId(by.GetAccessibilityId());
                        return elements;
                    }
                    else
                    {
                        var elements = this.windowsElement.FindElements(by.ToSeleniumBy());
                        return elements;
                    }
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
        public ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 5000)
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
        public ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 5000)
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
        public ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 5000)
        {
            return this.FindAll<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Send Key of the element.
        /// </summary>
        /// <param name="key">The Key to Send.</param>
        public void SendKeys(string key)
        {
            PerformAction((actions, windowElement) =>
            {
                windowElement.SendKeys(key);
            });
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

        /// <summary>
        /// Save UI Element to a PNG file.
        /// </summary>
        /// <param name="path">the full path</param>
        public void SaveToPngFile(string path)
        {
            Assert.IsNotNull(this.windowsElement, $"WindowsElement is null in method SaveToPngFile with parameter: path = {path}");
            this.windowsElement.GetScreenshot().SaveAsFile(path);
        }

        public void EnsureVisible(Element scrollViewer, int maxScrolls = 10)
        {
            int count = 0;
            if (scrollViewer.WindowsElement != null)
            {
                while (!this.windowsElement!.Displayed && count < maxScrolls)
                {
                    scrollViewer.WindowsElement.SendKeys(OpenQA.Selenium.Keys.PageDown);
                    Task.Delay(250).Wait();
                    count++;
                }
            }
        }
    }
}
