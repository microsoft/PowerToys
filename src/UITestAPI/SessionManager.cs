// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Microsoft.PowerToys.UITest
{
    public class SessionManager
    {
        public static Session? Current { get; private set; }

        private static string sessionPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

        private static Process? appDriver;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        private struct WinDriver
        {
            public Session Session { get; set; }

            public string AppName;
            public string WindowName;
        }

        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        private static Session? Root { get; set; }

        private static WinDriver CurrentDriver { get; set; }

        private static Stack<WinDriver> mWindowList = new Stack<WinDriver>();

        private static Stack<WinDriver> mWindowListTemp = new Stack<WinDriver>();

        protected SessionManager()
        {
        }

        static SessionManager()
        {
            if (mWindowList == null)
            {
                mWindowList = new Stack<WinDriver>();
            }

            if (mWindowListTemp == null)
            {
                mWindowListTemp = new Stack<WinDriver>();
            }

            var desktopCapabilities = new AppiumOptions();
            desktopCapabilities.AddAdditionalCapability("app", "Root");
            Root = new Session(new Uri(WindowsApplicationDriverUrl), desktopCapabilities);
            Current = Root;
        }

        public static void SetScope(PowerToysModule scope)
        {
            sessionPath = ModuleConfigData.Instance.GetModulePath(scope);
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        public static void Init()
        {
            appDriver = Process.Start("C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe");

            // Launch Exe
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path += sessionPath;
            StartExe("PowerToys", "PowerToys Settings", path);

            var session = Current;
            Assert.IsNotNull(session, "Session not initialized");

            // Set implicit timeout to make element search to retry every 500 ms
            session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        }

        public static void UnInit()
        {
            sessionPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";

            var session = Current;

            // Close the session
            if (session != null)
            {
                session.Quit();
                session.Dispose();
            }

            try
            {
                appDriver?.Kill();
            }
            catch
            {
            }
        }

        // Take control of an application that already exists
        public static Session? AttachSession(PowerToysModuleWindow module)
        {
            string windowName = ModuleConfigData.Instance.GetModuleWindowData(module).WindowName;
            string appName = ModuleConfigData.Instance.GetModuleWindowData(module).ModuleName;

            if (Root != null)
            {
                if (SwitchApp(appName) == true)
                {
                    return Current;
                }

                var window = Root.FindElementByName(windowName);
                if (window == null)
                {
                    Assert.IsNotNull(null, windowName + " not found");
                    return null;
                }

                var windowHandle = new nint(int.Parse(window.GetAttribute("NativeWindowHandle")));
                SetForegroundWindow(windowHandle);
                var hexWindowHandle = windowHandle.ToString("x");
                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                var appSession = new Session(new Uri(WindowsApplicationDriverUrl), appCapabilities);
                WinDriver winDriver = default;
                winDriver.Session = appSession;
                winDriver.AppName = appName;
                winDriver.WindowName = windowName;
                if (CurrentDriver.Session != null)
                {
                    mWindowList.Push(CurrentDriver);
                }

                CurrentDriver = winDriver;
                Current = appSession;
            }
            else
            {
                Assert.IsNotNull(Root, "Root driver is null");
            }

            return null;
        }

        // Create a new application and take control of it
        private static void StartExe(string appName, string windowName, string appPath)
        {
            AppiumOptions opts = new AppiumOptions();
            opts.AddAdditionalCapability("app", appPath);
            var session = new Session(new Uri(WindowsApplicationDriverUrl), opts);
            WinDriver winDriver = default;
            winDriver.Session = session;
            winDriver.AppName = appName;
            winDriver.WindowName = windowName;
            if (CurrentDriver.Session != null)
            {
                mWindowList.Push(CurrentDriver);
            }

            CurrentDriver = winDriver;
            Current = session;
        }

        // Use the name to switch the current driver
        private static bool SwitchApp(string appName)
        {
            while (mWindowList.Count > 0)
            {
                var driver = mWindowList.Peek();
                if (driver.AppName == appName)
                {
                    WinDriver driverTemp = mWindowList.Pop();
                    while (mWindowListTemp.Count > 0)
                    {
                        mWindowList.Push(mWindowListTemp.Pop());
                    }

                    // Check session is live
                    var elements = driverTemp.Session.FindElementsByAccessibilityId("elementId");
                    if (elements.Count <= 0)
                    {
                        return false;
                    }

                    mWindowList.Push(CurrentDriver);
                    CurrentDriver = driverTemp;
                    Current = CurrentDriver.Session;
                    if (CurrentDriver.Session != null)
                    {
                        var windowHandle = new nint(int.Parse(CurrentDriver.Session.FindElementByName(CurrentDriver.WindowName).GetAttribute("NativeWindowHandle")));
                        SetForegroundWindow(windowHandle);
                    }

                    return true;
                }
                else
                {
                    mWindowListTemp.Push(driver);
                    mWindowList.Pop();
                }
            }

            while (mWindowListTemp.Count > 0)
            {
                mWindowList.Push(mWindowListTemp.Pop());
            }

            return false;
        }
    }
}
