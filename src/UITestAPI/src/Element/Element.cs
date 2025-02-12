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
using static Microsoft.UITests.API.UITestBase;

namespace Microsoft.UITests.API
{
    public class Element
    {
        public WindowsElement? WindowsElement { get; set; }

        public Element()
        {
            WindowsElement = null;
        }

        public void SetWindowsElement(WindowsElement windowsElement)
        {
            WindowsElement = windowsElement;
        }

        public string GetName()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("Name");
        }

        public string GetText()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("Value");
        }

        public string GetAutomationId()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("AutomationId");
        }

        public string GetClassName()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("ClassName");
        }

        public bool IsEnable()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
            }

            return WindowsElement?.GetAttribute("IsEnabled") == "True" ? true : false;
        }

        public bool IsSelected()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
            }

            return WindowsElement?.GetAttribute("IsSelected") == "True" ? true : false;
        }

        public void Click()
        {
            WindowsDriverWrapper? session = UITestBase.Instance.GetCurrentWindow();
            var element = WindowsElement;
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.Click();
            actions.Build().Perform();
        }

        public void RightClick()
        {
            WindowsDriverWrapper? session = UITestBase.Instance.GetCurrentWindow();
            var element = WindowsElement;
            Actions actions = new Actions(session);
            actions.MoveToElement(element);
            actions.MoveByOffset(5, 5);
            actions.ContextClick();
            actions.Build().Perform();
        }

        public void ClickCheckAttribute(string attributeKey, string attributeValue)
        {
            WindowsDriverWrapper? session = UITestBase.Instance.GetCurrentWindow();
            var elements = WindowsElement;
            Actions actions = new Actions(session);
            if (elements?.GetAttribute(attributeKey) == attributeValue)
            {
                actions.MoveToElement(elements);
                actions.Click();
                actions.Build().Perform();
                actions.MoveByOffset(5, 5);
            }
        }

        public bool CheckAttribute(string attributeKey, string attributeValue)
        {
            var elements = WindowsElement;
            return elements?.GetAttribute(attributeKey) == attributeValue;
        }

        public T FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByName(name) as WindowsElement;
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

        public T? FindElementByAccessibilityId<T>(string name)
            where T : Element, new()
        {
            var item = WindowsElement?.FindElementByAccessibilityId(name) as WindowsElement;
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

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
