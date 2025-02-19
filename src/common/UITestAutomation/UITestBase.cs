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
    /// <summary>
    /// Base class that should be inherited by all Test Classes.
    /// </summary>
    public class UITestBase
    {
        public Session Session { get; set; }

        private readonly TestInit testInit = new TestInit();

        public UITestBase()
        {
            testInit.Init();
            Session = new Session(testInit.GetRoot(), testInit.GetDriver());
        }

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

        /// <summary>
        /// Nested class for test initialization.
        /// </summary>
        private sealed class TestInit
        {
            private WindowsDriver<WindowsElement> Root { get; set; }

            private WindowsDriver<WindowsElement>? Driver { get; set; }

            private static Process? appDriver;

            // Default session path is PowerToys settings dashboard
            private static string sessionPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

            public TestInit()
            {
                var desktopCapabilities = new AppiumOptions();
                desktopCapabilities.AddAdditionalCapability("app", "Root");
                Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);

                Root.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            }

            /// <summary>
            /// Initializes the test environment.
            /// </summary>
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

                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            }

            /// <summary>
            /// UnInitializes the test environment.
            /// </summary>
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

            /// <summary>
            /// Starts a new exe and takes control of it.
            /// </summary>
            /// <param name="appName">The name of the application.</param>
            /// <param name="windowName">The name of the window.</param>
            /// <param name="appPath">The path to the application executable.</param>
            public void StartExe(string appName, string windowName, string appPath)
            {
                var opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", appPath);
                Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), opts);
            }

            /// <summary>
            /// Sets scope to the Test Class.
            /// </summary>
            /// <param name="scope">The PowerToys module to start.</param>
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
