// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

        private static WindowsDriver<WindowsElement>? root;

        private WindowsDriver<WindowsElement>? Driver { get; set; }

        private static Process? appDriver;
        private Process? runner;

        private PowerToysModule scope;
        private string[]? commandLineArgs;

        /// <summary>
        /// Gets a value indicating whether to use installer paths for testing.
        /// </summary>
        private bool UseInstallerForTest { get; }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public SessionHelper(PowerToysModule scope, string[]? commandLineArgs = null)
        {
            this.scope = scope;
            this.commandLineArgs = commandLineArgs;
            this.sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
            UseInstallerForTest = EnvironmentConfig.UseInstallerForTest;
            this.locationPath = UseInstallerForTest ? string.Empty : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            CheckWinAppDriverAndRoot();
        }

        /// <summary>
        /// Initializes WinAppDriver And Root.
        /// </summary>
        public void CheckWinAppDriverAndRoot()
        {
            if (SessionHelper.root == null || SessionHelper.appDriver?.SessionId == null || SessionHelper.appDriver == null || SessionHelper.appDriver.HasExited)
            {
                this.StartWindowsAppDriverApp();
                var desktopCapabilities = new AppiumOptions();
                desktopCapabilities.AddAdditionalCapability("app", "Root");
                SessionHelper.root = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), desktopCapabilities);
            }
        }

        /// <summary>
        /// Initializes the test environment.
        /// </summary>
        /// <param name="scope">The PowerToys module to start.</param>
        public SessionHelper Init()
        {
            this.ExitExe(this.locationPath + this.sessionPath);

            this.StartExe(this.locationPath + this.sessionPath, this.commandLineArgs);

            Assert.IsNotNull(this.Driver, $"Failed to initialize the test environment. Driver is null.");

            return this;
        }

        /// <summary>
        /// Cleans up the test environment.
        /// </summary>
        public void Cleanup()
        {
            ExitScopeExe();
        }

        /// <summary>
        /// Exit a exe by Name.
        /// </summary>
        /// <param name="processName">The path to the application executable.</param>
        public void ExitExeByName(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
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
        /// Exit a exe.
        /// </summary>
        /// <param name="appPath">The path to the application executable.</param>
        public void ExitExe(string appPath)
        {
            // Exit Exe
            string exeName = Path.GetFileNameWithoutExtension(appPath);

            ExitExeByName(exeName);
        }

        /// <summary>
        /// Starts a new exe and takes control of it.
        /// </summary>
        /// <param name="appPath">The path to the application executable.</param>
        /// <param name="args">Optional command line arguments to pass to the application.</param>
        public void StartExe(string appPath, string[]? args = null, string? enableModules = null)
        {
            var opts = new AppiumOptions();
            if (!string.IsNullOrEmpty(enableModules))
            {
                opts.AddAdditionalCapability("enableModules", enableModules);
            }

            if (scope == PowerToysModule.PowerToysSettings)
            {
                TryLaunchPowerToysSettings(opts);
            }
            else if (scope == PowerToysModule.CommandPalette && UseInstallerForTest)
            {
                TryLaunchCommandPalette(opts);
            }
            else
            {
                opts.AddAdditionalCapability("app", appPath);

                if (args != null && args.Length > 0)
                {
                    // Build command line arguments string
                    string argsString = string.Join(" ", args.Select(arg =>
                    {
                        // Quote arguments that contain spaces
                        if (arg.Contains(' '))
                        {
                            return $"\"{arg}\"";
                        }

                        return arg;
                    }));

                    opts.AddAdditionalCapability("appArguments", argsString);
                }
            }

            Driver = NewWindowsDriver(opts);
        }

        private void TryLaunchPowerToysSettings(AppiumOptions opts)
        {
            if (opts.ToCapabilities().HasCapability("enableModules"))
            {
                var modulesString = (string)opts.ToCapabilities().GetCapability("enableModules");
                var modulesArray = modulesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                SettingsConfigHelper.ConfigureGlobalModuleSettings(modulesArray);
            }
            else
            {
                SettingsConfigHelper.ConfigureGlobalModuleSettings();
            }

            const int maxTries = 3;
            const int delayMs = 5000;
            const int maxRetries = 3;

            for (int tryCount = 1; tryCount <= maxTries; tryCount++)
            {
                try
                {
                    var runnerProcessInfo = new ProcessStartInfo
                    {
                        FileName = locationPath + runnerPath,
                        Verb = "runas",
                        Arguments = "--open-settings",
                    };

                    ExitExe(runnerProcessInfo.FileName);

                    // Verify process was killed
                    string exeName = Path.GetFileNameWithoutExtension(runnerProcessInfo.FileName);
                    var remainingProcesses = Process.GetProcessesByName(exeName);

                    runner = Process.Start(runnerProcessInfo);

                    if (WaitForWindowAndSetCapability(opts, "PowerToys Settings", delayMs, maxRetries))
                    {
                        // Exit CmdPal UI before launching new process if use installer for test
                        ExitExeByName("Microsoft.CmdPal.UI");
                        return;
                    }

                    // Window not found, kill all PowerToys processes and retry
                    if (tryCount < maxTries)
                    {
                        KillPowerToysProcesses();
                    }
                }
                catch (Exception ex)
                {
                    if (tryCount == maxTries)
                    {
                        throw new InvalidOperationException($"Failed to launch PowerToys Settings after {maxTries} attempts: {ex.Message}", ex);
                    }

                    // Kill processes and retry
                    KillPowerToysProcesses();
                }
            }

            throw new InvalidOperationException($"Failed to launch PowerToys Settings: Window not found after {maxTries} attempts.");
        }

        private void TryLaunchCommandPalette(AppiumOptions opts)
        {
            try
            {
                // Exit any existing CmdPal UI process
                ExitExeByName("Microsoft.CmdPal.UI");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c start shell:appsFolder\\Microsoft.CommandPalette_8wekyb3d8bbwe!App",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                var process = Process.Start(processStartInfo);
                process?.WaitForExit();

                if (!WaitForWindowAndSetCapability(opts, "Command Palette", 5000, 10))
                {
                    throw new TimeoutException("Failed to find Command Palette window after multiple attempts.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch Command Palette: {ex.Message}", ex);
            }
        }

        private bool WaitForWindowAndSetCapability(AppiumOptions opts, string windowName, int delayMs, int maxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var window = ApiHelper.FindDesktopWindowHandler(
                    [windowName, AdministratorPrefix + windowName]);

                if (window.Count > 0)
                {
                    var hexHwnd = window[0].HWnd.ToString("x");
                    opts.AddAdditionalCapability("appTopLevelWindow", hexHwnd);
                    return true;
                }

                if (attempt < maxRetries)
                {
                    Thread.Sleep(delayMs);
                }
            }

            return false;
        }

        /// <summary>
        /// Starts a new exe and takes control of it.
        /// </summary>
        /// <param name="info">The AppiumOptions for the application.</param>
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
                    CheckWinAppDriverAndRoot();
                }
            }
        }

        /// <summary>
        /// Exit now exe.
        /// </summary>
        public void ExitScopeExe()
        {
            ExitExe(sessionPath);
            try
            {
                if (this.scope == PowerToysModule.PowerToysSettings)
                {
                    runner?.Kill();
                    runner?.WaitForExit(); // Optional: Wait for the process to exit
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions if needed
                Console.WriteLine($"Exception during Cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Restarts now exe and takes control of it.
        /// </summary>
        public void RestartScopeExe(string? enableModules = null)
        {
            ExitScopeExe();
            StartExe(locationPath + sessionPath, commandLineArgs, enableModules);
        }

        public WindowsDriver<WindowsElement> GetRoot()
        {
            return SessionHelper.root!;
        }

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
            SessionHelper.appDriver = Process.Start(winAppDriverProcessInfo);
        }

        private void KillPowerToysProcesses()
        {
            var powerToysProcessNames = new[] { "PowerToys", "Microsoft.CmdPal.UI" };

            foreach (var processName in powerToysProcessNames)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);

                    foreach (var process in processes)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }

                    // Verify processes are actually gone
                    var remainingProcesses = Process.GetProcessesByName(processName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[KillPowerToysProcesses] Failed to kill process {processName}: {ex.Message}");
                }
            }
        }
    }
}
