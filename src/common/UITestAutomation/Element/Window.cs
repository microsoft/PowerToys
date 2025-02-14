// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;

namespace Microsoft.PowerToys.UITest
{
    // Class representing a window in the application
    public class Window : Element
    {
        // Class representing a window in the application
        public Window()
            : base()
        {
        }

        // Method to check if the window is visible
        public bool IsVisible()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            return WindowsElement.Displayed;
        }

        // Method to maximize the window
        public Window Maximize()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Maximize").Click();
            return this;
        }

        // Method to restore the window to its original size
        public Window Restore()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Restore").Click();
            return this;
        }

        // Method to minimize the window
        public Window Minimize()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            Assert.IsTrue(IsVisible(), "Window is not visible");
            FindElementByName<Button>("Minimize").Click();
            return this;
        }
    }
}
