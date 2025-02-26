// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Base class that should be inherited by all Test Classes.
    /// </summary>
    public class UITestBase
    {
        public Session Session { get; set; }

        private readonly SessionHelper sessionHelper;

        public UITestBase(PowerToysModule scope = PowerToysModule.PowerToysSettings, bool runAsAdmin = false)
        {
            this.sessionHelper = new SessionHelper(scope, runAsAdmin).Init();
            this.Session = new Session(this.sessionHelper.GetRoot(), this.sessionHelper.GetDriver());
        }

        ~UITestBase()
        {
            this.sessionHelper.Cleanup();
        }

        /// <summary>
        /// Finds an element by selector.
        /// Shortcut for this.Session.Find<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        protected T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Session.Find<T>(by, timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Session.FindAll<T>(by, timeoutMS);
        }
    }
}
