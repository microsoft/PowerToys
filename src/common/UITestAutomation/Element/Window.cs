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
    /// <summary>
    /// Represents a window in the UI test environment.
    /// </summary>
    public class Window : Element
    {
        public Window()
            : base()
        {
        }

        /// <summary>
        /// Clicks the Maximize button of the window.
        /// </summary>
        /// <returns>The current Window instance.</returns>
        public Window Maximize()
        {
            Find<Button>(By.Name("Maximize")).LeftClick();
            return this;
        }

        /// <summary>
        /// Clicks the Restore button of the window.
        /// </summary>
        /// <returns>The current Window instance.</returns>
        public Window Restore()
        {
            Find<Button>(By.Name("Restore")).LeftClick();
            return this;
        }

        /// <summary>
        /// Clicks the Minimize button of the window.
        /// </summary>
        /// <returns>The current Window instance.</returns>
        public Window Minimize()
        {
            Find<Button>(By.Name("Minimize")).LeftClick();
            return this;
        }

        /// <summary>
        /// Clicks the Close button of the window.
        /// </summary>
        public void Close()
        {
            Find<Button>(By.Name("Close")).LeftClick();
        }
    }
}
