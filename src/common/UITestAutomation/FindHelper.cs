// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

[assembly: InternalsVisibleTo("Element")]
[assembly: InternalsVisibleTo("Session")]

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Helper class for finding elements.
    /// </summary>
    internal static class FindHelper
    {
        public static ReadOnlyCollection<T>? FindAll<T, TW>(Func<ReadOnlyCollection<TW>> findElementsFunc, WindowsDriver<WindowsElement>? driver, int timeoutMS)
            where T : Element, new()
        {
            var items = findElementsFunc();
            var res = items.Select(item =>
            {
                var element = item as WindowsElement;
                return NewElement<T>(element, driver, timeoutMS);
            }).Where(item => item.IsMatchingTarget()).ToList();

            return new ReadOnlyCollection<T>(res);
        }

        public static T NewElement<T>(WindowsElement? element, WindowsDriver<WindowsElement>? driver, int timeoutMS)
             where T : Element, new()
        {
            Assert.IsNotNull(driver, $"New Element {typeof(T).Name} error: driver is null.");
            Assert.IsNotNull(element, $"New Element {typeof(T).Name} error: element is null.");

            T newElement = new T();
            if (timeoutMS > 0)
            {
                // Only set timeout if it is positive value
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMS);
            }

            newElement.SetSession(driver);
            newElement.SetWindowsElement(element);
            return newElement;
        }
    }
}
