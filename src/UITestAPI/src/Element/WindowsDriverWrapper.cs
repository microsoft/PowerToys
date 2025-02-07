// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.Interfaces;
using OpenQA.Selenium.Appium.Service;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Remote;

namespace Microsoft.UITests.API
{
#pragma warning disable SA1649 // File name should match first type name
    public class WindowsDriverWrapper : WindowsDriver<WindowsElement>
#pragma warning restore SA1649 // File name should match first type name
    {
        public WindowsDriverWrapper(Uri remoteAddress, AppiumOptions appiumOptions)
            : base(remoteAddress, appiumOptions)
        {
        }

        public T? FindElementByName<T>(string name)
            where T : Element, new()
        {
            var item = this.FindElementByName(name);
            Assert.IsNotNull(item, "Can`t find this element");
            T element = new T();
            return element;
        }
    }
}
