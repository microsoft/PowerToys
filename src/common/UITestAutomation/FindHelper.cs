// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
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
            var timeout = TimeSpan.FromMilliseconds(timeoutMS);
            var retryInterval = TimeSpan.FromMilliseconds(500);
            DateTime startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                IReadOnlyCollection<TW> items;
                try
                {
                    items = findElementsFunc();
                }
                catch (Exception)
                {
                    Task.Delay(retryInterval).Wait();
                    continue;
                }

                var result = new List<T>();
                foreach (TW item in items)
                {
                    try
                    {
                        T element = NewElement<T>(item as WindowsElement, driver, timeoutMS);
                        if (element.IsMatchingTarget())
                        {
                            result.Add(element);
                        }
                    }
                    catch (WebDriverException ex) when (IsStaleElement(ex))
                    {
                    }
                }

                if (result.Count > 0)
                {
                    return new ReadOnlyCollection<T>(result);
                }

                Task.Delay(retryInterval).Wait();
            }

            return new ReadOnlyCollection<T>(new List<T>());
        }

        private static bool IsStaleElement(WebDriverException exception)
        {
            return exception is StaleElementReferenceException ||
                   exception.Message.Contains("no longer attached to the DOM", StringComparison.OrdinalIgnoreCase) ||
                   exception.Message.Contains("stale element", StringComparison.OrdinalIgnoreCase);
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
