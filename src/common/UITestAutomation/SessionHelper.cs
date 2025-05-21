﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        private readonly string runnerPath = ModuleConfigData.Instance.GetModulePath(PowerToysModule.Runner);

        private string? locationPath;

        private WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement>? Driver { get; set; }

        private Process? appDriver;
        private Process? runner;

        private PowerToysModule scope;

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public SessionHelper(PowerToysModule scope)
        {
            this.scope = scope;
            this.sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            this.locationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var winAppDriverProcessInfo = new ProcessStartInfo
            {
                FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                Verb = "runas",
            };

            this.ExitExe(winAppDriverProcessInfo.FileName);
            this.appDriver = Process.Start(winAppDriverProcessInfo);

            var runnerProcessInfo = new ProcessStartInfo
            {
                FileName = locationPath + this.runnerPath,
                Verb = "runas",
            };

            if (scope == PowerToysModule.PowerToysSettings)
            {
                this.runner = Process.Start(runnerProcessInfo);
            }

            var desktopCapabilities = new AppiumOptions();
            desktopCapabilities.AddAdditionalCapability("app", "Root");
            this.Root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);
        }

        /// <summary>
        /// Initializes the test environment.
        /// </summary>
        /// <param name="scope">The PowerToys module to start.</param>
        public SessionHelper Init()
        {
            // Exit setting exe to fix CommandPalette error, remove after fixing the issue
            this.ExitExe(this.locationPath + ModuleConfigData.Instance.GetModulePath(PowerToysModule.PowerToysSettings));

            this.ExitExe(this.locationPath + this.sessionPath);
            this.StartExe(this.locationPath + this.sessionPath);

            Assert.IsNotNull(this.Driver, $"Failed to initialize the test environment. Driver is null.");

            return this;
        }

        /// <summary>
        /// Cleans up the test environment.
        /// </summary>
        public void Cleanup()
        {
            ExitScopeExe();
            try
            {
                appDriver?.Kill();
                appDriver?.WaitForExit(); // Optional: Wait for the process to exit
                if (this.scope == PowerToysModule.PowerToysSettings)
                {
                    runner?.Kill();
                    runner?.WaitForExit(); // Optional: Wait for the process to exit
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions if needed
                Debug.WriteLine($"Exception during Cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Exit a exe.
        /// </summary>
        /// <param name="appPath">The path to the application executable.</param>
        public void ExitExe(string appPath)
        {
            // Exit Exe
            string exeName = Path.GetFileNameWithoutExtension(appPath);

            Process[] processes = Process.GetProcessesByName(exeName);
            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(); // Optional: Wait for the process to exit
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to terminate process {process.ProcessName} (ID: {process.Id}): {ex.Message}");
                }
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

            // Create driver with retry
            var timeout = TimeSpan.FromMinutes(2);
            var retryInterval = TimeSpan.FromSeconds(5);
            DateTime startTime = DateTime.Now;

            while (true)
            {
                try
                {
                    this.Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), opts);
                    break;
                }
                catch (Exception)
                {
                    if (DateTime.Now - startTime > timeout)
                    {
                        throw;
                    }

                    Task.Delay(retryInterval).Wait();
                }
            }
        }

        /// <summary>
        /// Exit now exe.
        /// </summary>
        public void ExitScopeExe()
        {
            ExitExe(sessionPath);
        }

        /// <summary>
        /// Restarts now exe and takes control of it.
        /// </summary>
        public void RestartScopeExe()
        {
            ExitExe(sessionPath);
            StartExe(locationPath + sessionPath);
        }

        public WindowsDriver<WindowsElement> GetRoot() => this.Root;

        public WindowsDriver<WindowsElement> GetDriver()
        {
            Assert.IsNotNull(this.Driver, $"Failed to get driver. Driver is null.");
            return this.Driver;
        }
    }
}
