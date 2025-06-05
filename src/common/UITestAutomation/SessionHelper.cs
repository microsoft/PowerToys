// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using static Microsoft.PowerToys.UITest.WindowHelper;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Nested class for test initialization.
    /// </summary>
    public class SessionHelper
    {
        // Default session path is PowerToys settings dashboard
        private readonly string sessionPath = ModuleConfigData.Instance.GetModulePath(PowerToysModule.PowerToysSettings);

        private readonly string runnerPath = ModuleConfigData.Instance.GetModulePath(PowerToysModule.Runner);

        private string? locationPath;

        private WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement>? Driver { get; set; }

        private Process? appDriver;
        private Process? runner;

        public PowerToysModule Scope { get; private set; }

        public WindowSize Size { get; private set; }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public SessionHelper(PowerToysModule scope, WindowSize size)
        {
            this.Scope = scope;
            this.Size = size;
            this.sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            this.locationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.StartWindowsAppDriverApp();

            var runnerProcessInfo = new ProcessStartInfo
            {
                FileName = locationPath + this.runnerPath,
                Verb = "runas",
            };

            if (scope == PowerToysModule.PowerToysSettings)
            {
                this.ExitExe(runnerProcessInfo.FileName);
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
            this.Root.Quit();
            ExitScopeExe();
            try
            {
                appDriver?.Kill();
                appDriver?.WaitForExit(); // Optional: Wait for the process to exit
                if (this.Scope == PowerToysModule.PowerToysSettings)
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
            this.Driver = NewWindowsDriver(opts);
        }

        /// <summary>
        /// Starts a new exe and takes control of it.
        /// </summary>
        /// <param name="info">The path to the application executable.</param>
        private WindowsDriver<WindowsElement> NewWindowsDriver(AppiumOptions info)
        {
            // Create driver with retry
            var timeout = TimeSpan.FromMinutes(2);
            var retryInterval = TimeSpan.FromSeconds(5);
            DateTime startTime = DateTime.Now;

            while (true)
            {
                try
                {
                    var res = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), info);
                    return res;
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
        /// Start scope exe.
        /// </summary>
        public void StartScopeExe()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = this.locationPath + this.sessionPath,
                Arguments = string.Empty,
                UseShellExecute = true,
            };
            Process.Start(processInfo);
            string windowName = ModuleConfigData.Instance.GetModuleWindowName(this.Scope);
            var windowHandleInfo = this.Attach(windowName, this.Size);
        }

        /// <summary>
        /// Exit scope exe.
        /// </summary>
        public void ExitScopeExe()
        {
            if (this.Driver != null)
            {
                // If the driver is already initialized, quit it before starting a new one
                this.Driver.Quit();
                this.Driver = null;
            }
        }

        /// <summary>
        /// Restarts now exe and takes control of it.
        /// </summary>
        public void RestartScopeExe()
        {
            ExitScopeExe();
            StartScopeExe();
        }

        public WindowsDriver<WindowsElement> GetRoot() => this.Root;

        public WindowsDriver<WindowsElement> GetDriver()
        {
            Assert.IsNotNull(this.Driver, $"Failed to get driver. Driver is null.");
            return this.Driver;
        }

        private void StartWindowsAppDriverApp()
        {
            var winAppDriverProcessInfo = new ProcessStartInfo
            {
                FileName = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe",
                Verb = "runas",
            };

            this.ExitExe(winAppDriverProcessInfo.FileName);
            this.appDriver = Process.Start(winAppDriverProcessInfo);
        }

        /// <summary>
        /// Attaches to an existing exe by string window name.
        /// The session should be attached when a new app is started.
        /// </summary>
        /// <param name="windowName">The window name to attach to.</param>
        /// <param name="size">The window size to set. Default is no change to window size</param>
        public WindowHandleInfo Attach(string windowName, WindowSize size = WindowSize.UnSpecified)
        {
            WindowHandleInfo res = new WindowHandleInfo { };
            if (this.Root != null)
            {
                // search window handler by window title (admin and non-admin titles)
                var timeout = TimeSpan.FromMinutes(2);
                var retryInterval = TimeSpan.FromSeconds(5);
                DateTime startTime = DateTime.Now;

                List<(IntPtr HWnd, string Title)>? matchingWindows = null;

                while (DateTime.Now - startTime < timeout)
                {
                    matchingWindows = WindowHelper.ApiHelper.FindDesktopWindowHandler(
                    new[] { windowName, WindowHelper.AdministratorPrefix + windowName });

                    if (matchingWindows.Count > 0 && matchingWindows[0].HWnd != IntPtr.Zero)
                    {
                        break;
                    }

                    Task.Delay(retryInterval).Wait();
                }

                if (matchingWindows == null || matchingWindows.Count == 0 || matchingWindows[0].HWnd == IntPtr.Zero)
                {
                    Assert.Fail($"Failed to attach. Window '{windowName}' not found after {timeout.TotalSeconds} seconds.");
                }

                // pick one from matching windows
                res.MainWindowHandler = matchingWindows[0].HWnd;
                res.MainWindowTitle = matchingWindows[0].Title;
                res.IsElevated = matchingWindows[0].Title.StartsWith(WindowHelper.AdministratorPrefix);

                ApiHelper.SetForegroundWindow(res.MainWindowHandler);

                var hexWindowHandle = res.MainWindowHandler.ToInt64().ToString("x");

                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                this.Driver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), appCapabilities);

                if (size != WindowSize.UnSpecified)
                {
                    WindowHelper.SetWindowSize(res.MainWindowHandler, size);
                }
            }
            else
            {
                Assert.IsNotNull(this.Root, $"Failed to attach to the window '{windowName}'. Root driver is null");
            }

            Task.Delay(3000).Wait();
            return res;
        }
    }

    public struct WindowHandleInfo
    {
        public IntPtr MainWindowHandler { get; set; }

        public string MainWindowTitle { get; set; }

        public bool IsElevated { get; set; }
    }
}
