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
    public class Window : Element
    {
        public Window()
            : base()
        {
        }

        // Click the Maximize button of the window
        public Window Maximize()
        {
            FindElementByName<Button>("Maximize").Click();
            return this;
        }

        // Click the Restore button of the window
        public Window Restore()
        {
            FindElementByName<Button>("Restore").Click();
            return this;
        }

        // Click the Minimize button of the window
        public Window Minimize()
        {
            FindElementByName<Button>("Minimize").Click();
            return this;
        }

        // Click the Close button of the window
        public void Close()
        {
            FindElementByName<Button>("Close").Click();
        }
    }
}
