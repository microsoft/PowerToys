// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Base class that should be inherited by all Test Classes.
    /// </summary>
    public class UITestBase
    {
        public Session Session { get; set; }

        private readonly TestInit testInit = new TestInit();

        public UITestBase(PowerToysModule scope = PowerToysModule.PowerToysSettings)
        {
            this.testInit.SetScope(scope);
            this.testInit.Init();
            this.Session = new Session(this.testInit.GetRoot(), this.testInit.GetDriver());
        }

        ~UITestBase()
        {
            this.testInit.Cleanup();
        }

        /// <summary>
        /// Finds an element by selector.
        /// Shortcut for this.Session.Find<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>The found element.</returns>
        protected T Find<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Session.Find<T>(by, timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 3000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 3000)
            where T : Element, new()
        {
            return this.Session.FindAll<T>(by, timeoutMS);
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
            private static string sessionPath = ModuleConfigData.Instance.GetModulePath(PowerToysModule.PowerToysSettings);

            public TestInit()
            {
                appDriver = Process.Start(new ProcessStartInfo
                {
                    FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                    Verb = "runas",
                });

                var desktopCapabilities = new AppiumOptions();
                desktopCapabilities.AddAdditionalCapability("app", "Root");
                this.Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);

                // Set default timeout to 5 seconds
                this.Root.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            }

            /// <summary>
            /// Initializes the test environment.
            /// </summary>
            [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
            public void Init()
            {
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                this.StartExe(path + sessionPath);

                Assert.IsNotNull(this.Driver, $"Failed to initialize the test environment. Driver is null.");

                // Set default timeout to 5 seconds
                this.Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            }

            /// <summary>
            /// Cleans up the test environment.
            /// </summary>
            public void Cleanup()
            {
                try
                {
                    appDriver?.Kill();
                }
                catch (Exception ex)
                {
                    // Handle exceptions if needed
                    Debug.WriteLine($"Exception during Cleanup: {ex.Message}");
                }
            }

            /// <summary>
            /// Starts a new exe and takes control of it.
            /// </summary>
            /// <param name="appPath">The path to the application executable.</param>
            public void StartExe(string appPath)
            {
                var opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", appPath);
                this.Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), opts);
            }

            /// <summary>
            /// Sets scope to the Test Class.
            /// </summary>
            /// <param name="scope">The PowerToys module to start.</param>
            public void SetScope(PowerToysModule scope)
            {
                sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            }

            public WindowsDriver<WindowsElement> GetRoot() => this.Root;

            public WindowsDriver<WindowsElement> GetDriver()
            {
                Assert.IsNotNull(this.Driver, $"Failed to get driver. Driver is null.");
                return this.Driver;
            }
        }
    }
}
