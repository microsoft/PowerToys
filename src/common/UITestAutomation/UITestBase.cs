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
    // Base class for UI tests
    public class UITestBase
    {
        // Property to hold the session
        public Session Session { get; set; }

        // Instance of TestInit class
        private readonly TestInit testInit = new TestInit();

        // Default constructor
        public UITestBase()
        {
            testInit.Init();
            Session = new Session(testInit.GetRoot(), testInit.GetDriver());
        }

        // Constructor with scope parameter
        public UITestBase(PowerToysModule scope)
        {
            testInit.SetScope(scope);
            testInit.Init();
            Session = new Session(testInit.GetRoot(), testInit.GetDriver());
        }

        // Destructor to uninitialize test
        ~UITestBase()
        {
            testInit.UnInit();
        }

        // Nested class for test initialization
        private sealed class TestInit
        {
            // Property to hold the root driver
            private WindowsDriver<WindowsElement> Root { get; set; }

            // Property to hold the driver
            private WindowsDriver<WindowsElement>? Driver { get; set; }

            // Static field to hold the application driver process
            private static Process? appDriver;

            // Static field to hold the session path
            private static string sessionPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

            // Constructor to initialize the root driver
            public TestInit()
            {
                var desktopCapabilities = new AppiumOptions();
                desktopCapabilities.AddAdditionalCapability("app", "Root");
                Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);
            }

            // Method to initialize the test
            [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
            public void Init()
            {
                appDriver = Process.Start(new ProcessStartInfo
                {
                    FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                    Verb = "runas",
                });

                // Launch the executable
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += sessionPath;
                StartExe("PowerToys", "PowerToys Settings", path);

                Assert.IsNotNull(Driver, "Session not initialized");

                // Set implicit timeout to make element search retry every 500 ms
                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
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

            // Create a new application and take control of it
            public void StartExe(string appName, string windowName, string appPath)
            {
                var opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", appPath);
                Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), opts);
            }

            // Method to set the scope of the test
            public void SetScope(PowerToysModule scope)
            {
                sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            }

            // Method to get the root driver
            public WindowsDriver<WindowsElement> GetRoot() => Root;

            // Method to get the driver
            public WindowsDriver<WindowsElement> GetDriver()
            {
                Assert.IsNotNull(Driver);
                return Driver;
            }
        }
    }
}
