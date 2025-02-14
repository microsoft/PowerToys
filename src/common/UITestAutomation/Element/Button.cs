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
    public class Button : Element
    {
        public Button()
            : base()
        {
        }

        // Get Button Type
        public string GetButtonType()
        {
            Assert.IsNotNull(WindowsElement, "WindowsElement should not be null");
            return WindowsElement.GetAttribute("ControlType");
        }
    }
}
