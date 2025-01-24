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

namespace Microsoft.UITests.API
{
    public class UIManager
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        public struct WinDriver
        {
            public WindowsDriver<WindowsElement>? Session { get; set; }

            public string AppName;
            public string WindowName;
        }

        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        public WindowsDriver<WindowsElement>? Root { get; private set; }

        public WinDriver CurrentDriver { get; private set; }

        private Stack<WinDriver> mWindowList = new Stack<WinDriver>();

        private Stack<WinDriver> mWindowListTemp = new Stack<WinDriver>();

        public UIManager()
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
            Root = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), desktopCapabilities);
        }

        // Create a new application and take control of it
        public void StartApp(string appName, string windowName, string appPath)
        {
            AppiumOptions opts = new AppiumOptions();
            opts.AddAdditionalCapability("app", appPath);
            var session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
            WinDriver winDriver = default(WinDriver);
            winDriver.Session = session;
            winDriver.AppName = appName;
            winDriver.WindowName = windowName;
            if (CurrentDriver.Session != null)
            {
                mWindowList.Push(CurrentDriver);
            }

            CurrentDriver = winDriver;
        }

        // Take control of an application that already exists
        public void LaunchApp(string appName, string windowName)
        {
            if (Root != null)
            {
                var window = Root.FindElementByName(windowName);
                if (window == null)
                {
                    Assert.IsNotNull(null, windowName + " not found");
                    return;
                }

                var windowHandle = new IntPtr(int.Parse(window.GetAttribute("NativeWindowHandle")));
                SetForegroundWindow(windowHandle);
                var hexWindowHandle = windowHandle.ToString("x");
                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                var appSession = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appCapabilities);
                WinDriver winDriver = default(WinDriver);
                winDriver.Session = appSession;
                winDriver.AppName = appName;
                winDriver.WindowName = windowName;
                if (CurrentDriver.Session != null)
                {
                    mWindowList.Push(CurrentDriver);
                }

                CurrentDriver = winDriver;
            }
            else
            {
                Assert.IsNotNull(null, "Root driver is null");
            }

            return;
        }

        // Use the name to switch the current driver
        public void SwitchApp(string appName)
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

                    mWindowList.Push(CurrentDriver);
                    CurrentDriver = driverTemp;
                    if (CurrentDriver.Session != null)
                    {
                        var windowHandle = new IntPtr(int.Parse(CurrentDriver.Session.FindElementByName(CurrentDriver.WindowName).GetAttribute("NativeWindowHandle")));
                        SetForegroundWindow(windowHandle);
                    }

                    return;
                }
                else
                {
                    mWindowListTemp.Push(driver);
                    mWindowList.Pop();
                }
            }

            Assert.IsNotNull(null, "appName not found");
        }

        public void CloseApp(string appName)
        {
            if (CurrentDriver.AppName == appName)
            {
                if (mWindowList.Count <= 0)
                {
                    return;
                }

                CurrentDriver = mWindowList.Pop();
                return;
            }

            while (mWindowList.Count > 0)
            {
                var driver = mWindowList.Peek();
                if (driver.AppName == appName)
                {
                    mWindowList.Pop();
                    while (mWindowListTemp.Count > 0)
                    {
                        mWindowList.Push(mWindowListTemp.Pop());
                    }

                    return;
                }
                else
                {
                    mWindowListTemp.Push(driver);
                    mWindowList.Pop();
                }
            }

            Assert.IsNotNull(null, "appName not found");
        }

        public WindowsDriver<WindowsElement>? GetCurrentWindow()
        {
            return CurrentDriver.Session;
        }

        public WindowsDriver<WindowsElement>? GetWindowInList(string appName)
        {
            while (mWindowList.Count > 0)
            {
                var driver = mWindowList.Peek();
                if (driver.AppName == appName)
                {
                    while (mWindowListTemp.Count > 0)
                    {
                        mWindowList.Push(mWindowListTemp.Pop());
                    }

                    return driver.Session;
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

            return null;
        }

        private static WindowsElement GetWindow(WindowsElement window)
        {
            return window;
        }
    }
}
