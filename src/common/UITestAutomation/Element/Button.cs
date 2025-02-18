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
    // Represents a button in the UI test framework.
    public class Button : Element
    {
        public Button()
            : base()
        {
        }

        // Gets the type of the button.
        // Returns:
        //   string: The control type of the button as a string.
        public string GetButtonType()
        {
            return GetAttribute("ControlType");
        }
    }
}
