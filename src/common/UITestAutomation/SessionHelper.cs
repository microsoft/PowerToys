// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Nested class for test initialization.
    /// </summary>
    internal class SessionHelper
    {
        // Default session path is PowerToys settings dashboard
        private readonly string sessionPath = ModuleConfigData.Instance.GetModulePath(PowerToysModule.PowerToysSettings);

        private WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement>? Driver { get; set; }

        private Process? appDriver;

        public SessionHelper(PowerToysModule scope)
        {
            this.sessionPath = ModuleConfigData.Instance.GetModulePath(scope);

            var winAppDriverProcessInfo = new ProcessStartInfo
            {
                FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                Verb = "runas",
            };

            this.appDriver = Process.Start(winAppDriverProcessInfo);

            var desktopCapabilities = new AppiumOptions();
            desktopCapabilities.AddAdditionalCapability("app", "Root");
            this.Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);

            // Set default timeout to 5 seconds
            this.Root.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Initializes the test environment.
        /// </summary>
        /// <param name="scope">The PowerToys module to start.</param>
        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public SessionHelper Init()
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            this.StartExe(path + this.sessionPath);

            Assert.IsNotNull(this.Driver, $"Failed to initialize the test environment. Driver is null.");

            // Set default timeout to 5 seconds
            this.Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

            return this;
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

        public WindowsDriver<WindowsElement> GetRoot() => this.Root;

        public WindowsDriver<WindowsElement> GetDriver()
        {
            Assert.IsNotNull(this.Driver, $"Failed to get driver. Driver is null.");
            return this.Driver;
        }
    }
}
