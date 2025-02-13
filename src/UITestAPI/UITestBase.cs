// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.PowerToys.UITest.ModuleConfigData;
using static Microsoft.PowerToys.UITest.UITestBase;

namespace Microsoft.PowerToys.UITest
{
    public class UITestBase
    {
        public Session? Session { get; set; }

        public UITestBase()
        {
            SessionManager.Init();
        }

        public UITestBase(PowerToysModule scope)
        {
            SessionManager.SetScope(scope);
            SessionManager.Init();
        }

        ~UITestBase()
        {
            SessionManager.UnInit();
        }

        public static void Enable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            Session? session = SessionManager.Current;
            var elements = session?.FindElementsByName<Element>("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element.CheckAttribute("HelpText", moduleName))
                    {
                        if (element.CheckAttribute("Toggle.ToggleState", "0"))
                        {
                            element.Click();
                        }

                        buttonFound = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }

        public static void Disable_Module_from_Dashboard(string moduleName, PowerToysModuleWindow module = PowerToysModuleWindow.None)
        {
            Session? session = SessionManager.Current;
            var elements = session?.FindElementsByName<Element>("Enable module");
            Actions actions = new Actions(session);
            bool buttonFound = false;
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element.CheckAttribute("HelpText", moduleName))
                    {
                        if (element.CheckAttribute("Toggle.ToggleState", "1"))
                        {
                            element.Click();
                        }

                        buttonFound = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(buttonFound, $"No button with elementName '{moduleName}' and HelpText '{moduleName}' was found.");
        }
    }
}
