// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    // Base class that should be interited by all Test Classes
    public class UITestBase
    {
        public Session Session { get; set; }

        private readonly TestInit testInit = new TestInit();

        public UITestBase()
        {
            testInit.Init();
            Session = new Session(testInit.GetRoot(), testInit.GetDriver());
        }

        // The default scope starts from the PowerToys settings UI.Set scope here to specify starting module
        public UITestBase(PowerToysModule scope)
        {
            testInit.SetScope(scope);
            testInit.Init();
            Session = new Session(testInit.GetRoot(), testInit.GetDriver());
        }

        ~UITestBase()
        {
            testInit.UnInit();
        }

        // Nested class for test initialization
        private sealed class TestInit
        {
            private WindowsDriver<WindowsElement> Root { get; set; }

            private WindowsDriver<WindowsElement>? Driver { get; set; }

            private static Process? appDriver;

            // Default session path
            private static string sessionPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

            public TestInit()
            {
                var desktopCapabilities = new AppiumOptions();
                desktopCapabilities.AddAdditionalCapability("app", "Root");
                Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);

                // Set implicit timeout to make element search retry every 500 ms
                Root.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            }

            // Initialize WinAppDriver
            [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
            public void Init()
            {
                appDriver = Process.Start(new ProcessStartInfo
                {
                    FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                    Verb = "runas",
                });

                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += sessionPath;
                StartExe("PowerToys", "PowerToys Settings", path);

                Assert.IsNotNull(Driver, "Session not initialized");

                // Set implicit timeout to make element search retry every 500 ms
                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            }

            // Method to uninitialize the test
            public void UnInit()
            {
                try
                {
                    appDriver?.Kill();
                }
                catch
                {
                    // Handle exceptions if needed
                }
            }

            // Start a new exe and take control of it
            public void StartExe(string appName, string windowName, string appPath)
            {
                var opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", appPath);
                Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), opts);
            }

            // Set scope to the Test Class
            public void SetScope(PowerToysModule scope)
            {
                sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            }

            public WindowsDriver<WindowsElement> GetRoot() => Root;

            public WindowsDriver<WindowsElement> GetDriver()
            {
                Assert.IsNotNull(Driver);
                return Driver;
            }
        }
    }
}
