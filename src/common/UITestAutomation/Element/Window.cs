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
        /// <summary>
        /// Maximizes the window.
        /// </summary>
        /// <param name="byClickButton">If true, clicks the Maximize button; otherwise, sets the window state.</param>
        /// <returns>The current Window instance.</returns>
        public Window Maximize(bool byClickButton = true)
        {
            if (byClickButton)
            {
                Find<Button>(By.Name("Maximize")).Click();
            }
            else
            {
                // TODO: Implement maximizing the window using an alternative method
            }

            return this;
        }

        /// <summary>
        /// Restores the window.
        /// </summary>
        /// <param name="byClickButton">If true, clicks the Restore button; otherwise, sets the window state.</param>
        /// <returns>The current Window instance.</returns>
        public Window Restore(bool byClickButton = true)
        {
            if (byClickButton)
            {
                Find<Button>(By.Name("Restore")).Click();
            }
            else
            {
                // TODO: Implement restoring the window using an alternative method
            }

            return this;
        }

        /// <summary>
        /// Minimizes the window.
        /// </summary>
        /// <param name="byClickButton">If true, clicks the Minimize button; otherwise, sets the window state.</param>
        /// <returns>The current Window instance.</returns>
        public Window Minimize(bool byClickButton = true)
        {
            if (byClickButton)
            {
                Find<Button>(By.Name("Minimize")).Click();
            }
            else
            {
                // TODO: Implement minimizing the window using an alternative method
            }

            return this;
        }

        /// <summary>
        /// Closes the window.
        /// </summary>
        /// <param name="byClickButton">If true, clicks the Close button; otherwise, closes the window using an alternative method.</param>
        public void Close(bool byClickButton = true)
        {
            if (byClickButton)
            {
                Find<Button>(By.Name("Close")).Click();
            }
            else
            {
                // TODO: Implement closing the window using an alternative method
            }
        }
    }
}
