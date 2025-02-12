// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;

namespace Microsoft.UITests.API
{
    public class Window : Element
    {
        public Window()
            : base()
        {
        }

        public string GetHelpText()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("HelpText");
        }

        public bool IsVisible()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            return WindowsElement.Displayed;
        }

        public Window Maximize()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Maximize").Click();
            return this;
        }

        public Window Restore()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Restore").Click();
            return this;
        }

        public Window Minimize()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Minimize").Click();
            return this;
        }
    }
}
