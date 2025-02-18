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
    // Represents a window in the UI test framework.
    public class Window : Element
    {
        public Window()
            : base()
        {
        }

        // Click the Maximize button of the window.
        // Returns:
        //   Window: The current Window instance.
        public Window Maximize()
        {
            Find<Button>(By.Name("Maximize")).LeftClick();
            return this;
        }

        // Click the Restore button of the window.
        // Returns:
        //   Window: The current Window instance.
        public Window Restore()
        {
            Find<Button>(By.Name("Restore")).LeftClick();
            return this;
        }

        // Click the Minimize button of the window.
        // Returns:
        //   Window: The current Window instance.
        public Window Minimize()
        {
            Find<Button>(By.Name("Minimize")).LeftClick();
            return this;
        }

        // Returns:
        //   Window: The current Window instance.
        public void Close()
        {
            Find<Button>(By.Name("Close")).LeftClick();
        }
    }
}
