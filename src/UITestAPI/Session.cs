// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.Interfaces;
using OpenQA.Selenium.Appium.Service;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Remote;

namespace Microsoft.PowerToys.UITest
{
    public class Session : WindowsDriver<WindowsElement>
    {
        public Session(Uri remoteAddress, AppiumOptions appiumOptions)
            : base(remoteAddress, appiumOptions)
        {
        }

        public T FindElement<T>(By by)
             where T : Element, new()
        {
            var item = FindElement(by.ToSeleniumBy());
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

        public T FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = FindElementByName(name);
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            element.SetWindowsElement(item);
            return element;
        }

        public ReadOnlyCollection<T>? FindElementsByName<T>(string name)
            where T : Element, new()
        {
            var items = FindElementsByName(name);
            Assert.IsNotNull(items, "Can`t find this element");
            List<T> res = new List<T>();
            foreach (var item in items)
            {
                T element = new T();
                element.SetWindowsElement(item);
                res.Add(element);
            }

            var resReadOnlyCollection = new ReadOnlyCollection<T>(res);
            return resReadOnlyCollection;
        }
    }
}
