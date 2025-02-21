// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

[assembly: InternalsVisibleTo("Element")]
[assembly: InternalsVisibleTo("Session")]

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Helper class for finding elements.
    /// </summary>
    internal static class FindElementHelper
    {
        public static T Find<T, TW>(Func<TW> findElementFunc, WindowsDriver<WindowsElement>? driver, int timeoutMS)
            where T : Element, new()
        {
            var item = findElementFunc() as WindowsElement;
            return NewElement<T>(item, driver, timeoutMS);
        }

        public static ReadOnlyCollection<T>? FindAll<T, TW>(Func<ReadOnlyCollection<TW>> findElementsFunc, WindowsDriver<WindowsElement>? driver, int timeoutMS)
            where T : Element, new()
        {
            var items = findElementsFunc();
            var res = items.Select(item =>
            {
                var element = item as WindowsElement;
                return NewElement<T>(element, driver, timeoutMS);
            }).ToList();

            return new ReadOnlyCollection<T>(res);
        }

        public static T NewElement<T>(WindowsElement? element, WindowsDriver<WindowsElement>? driver, int timeoutMS)
             where T : Element, new()
        {
            Assert.IsNotNull(driver, $"New Element {typeof(T).Name} error: driver is null.");
            Assert.IsNotNull(element, $"New Element {typeof(T).Name} error: element is null.");

            T newElement = new T();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMS);
            newElement.SetSession(driver);
            newElement.SetWindowsElement(element);
            return newElement;
        }
    }
}
