// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;

namespace Microsoft.UITests.API
{
    public class Window : Element
    {
        public Window()
            : base()
        {
        }

        public string GetHelpText()
        {
            if (WindowsElement == null)
            {
                Assert.IsNotNull(null);
                return " ";
            }

            return WindowsElement.GetAttribute("HelpText");
        }
    }
}
