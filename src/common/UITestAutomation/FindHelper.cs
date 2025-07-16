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
        public static ReadOnlyCollection<T>? FindAll<T, TW>(Func<IReadOnlyCollection<TW>> findElementsFunc, WindowsDriver<WindowsElement>? driver, int timeoutMS)
            where T : Element, new()
        {
            var items = FindElementsWithRetry(findElementsFunc, timeoutMS);
            var res = items.Select(item =>
            {
                var element = item as WindowsElement;
                return NewElement<T>(element, driver, timeoutMS);
            }).Where(item => item.IsMatchingTarget()).ToList();

            return new ReadOnlyCollection<T>(res);
        }

        private static ReadOnlyCollection<TW> FindElementsWithRetry<TW>(Func<IReadOnlyCollection<TW>> findElementsFunc, int timeoutMS)
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMS);
            var retryIntervalMS = TimeSpan.FromMilliseconds(500);
            DateTime startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    var items = findElementsFunc();
                    if (items.Count > 0)
                    {
                        return new ReadOnlyCollection<TW>((IList<TW>)items);
                    }

                    Task.Delay(retryIntervalMS).Wait();
                }
                catch (Exception)
                {
                }
            }

            return new ReadOnlyCollection<TW>(new List<TW>());
        }

        public static T NewElement<T>(WindowsElement? element, WindowsDriver<WindowsElement>? driver, int timeoutMS)
             where T : Element, new()
        {
            Assert.IsNotNull(driver, $"New Element {typeof(T).Name} error: driver is null.");
            Assert.IsNotNull(element, $"New Element {typeof(T).Name} error: element is null.");

            T newElement = new T();

            newElement.SetSession(driver);
            newElement.SetWindowsElement(element);
            return newElement;
        }
    }
}
