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
    internal static class FindElementHelper
    {
        public static T FindElement<T, TW>(Func<TW> findElementFunc, int timeoutMS, WindowsDriver<WindowsElement>? driver)
            where T : Element, new()
        {
            Assert.IsNotNull(driver, "driver is null");
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMS);
            var item = findElementFunc() as WindowsElement;
            Assert.IsNotNull(item, "Can't find this element");

            return NewElement<T>(item, driver);
        }

        public static ReadOnlyCollection<T>? FindElements<T, TW>(Func<ReadOnlyCollection<TW>> findElementsFunc, int timeoutMS, WindowsDriver<WindowsElement>? driver)
            where T : Element, new()
        {
            Assert.IsNotNull(driver, "driver is null");
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMS);
            var items = findElementsFunc();
            var res = items.Select(item =>
            {
                var element = item as WindowsElement;
                if (element != null)
                {
                    NewElement<T>(element, driver);
                }

                return element;
            }).Where(element => element != null).ToList();

            return new ReadOnlyCollection<T>((IList<T>)res);
        }

        // Create a new element of type T
        public static T NewElement<T>(WindowsElement element, WindowsDriver<WindowsElement>? driver)
             where T : Element, new()
        {
            T newElement = new T();
            Assert.IsNotNull(driver, "[FindElementHelper.cs] driver is null");
            newElement.SetSession(driver);
            Assert.IsNotNull(element, "[FindElementHelper] element is null");
            newElement.SetWindowsElement(element);
            return newElement;
        }
    }
}
