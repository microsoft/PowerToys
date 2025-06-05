// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using static Microsoft.PowerToys.UITest.WindowHelper;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides interfaces for interacting with UI elements.
    /// </summary>
    public class Session
    {
        public WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement> WindowsDriver { get; set; }

        private List<IntPtr> windowHandlers = new List<IntPtr>();

        private Window? MainWindow { get; set; }

        /// <summary>
        /// Gets Main Window Handler
        /// </summary>
        public IntPtr MainWindowHandler { get; private set; }

        /// <summary>
        /// Gets Init Scope
        /// </summary>
        public PowerToysModule InitScope { get; private set; }

        /// <summary>
        /// Gets the RunAsAdmin flag.
        /// If true, the session is running as admin.
        /// If false, the session is not running as admin.
        /// If null, no information is available.
        /// </summary>
        public bool? IsElevated { get; private set; }

        public Session(WindowsDriver<WindowsElement> pRoot, WindowsDriver<WindowsElement> pDriver, PowerToysModule scope, WindowSize size)
        {
            this.MainWindowHandler = IntPtr.Zero;
            this.Root = pRoot;
            this.WindowsDriver = pDriver;
            this.InitScope = scope;

            if (size != WindowSize.UnSpecified)
            {
                // Attach to the scope & reset MainWindowHandler
                this.Attach(scope, size);
            }
        }

        /// <summary>
        /// Cleans up the Session Exe.
        /// </summary>
        public void Cleanup()
        {
            windowHandlers.Clear();
        }

        /// <summary>
        /// Finds an Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public T Find<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsDriver, $"WindowsElement is null in method Find<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");

            // leverage findAll to filter out mismatched elements
            var collection = this.FindAll<T>(by, timeoutMS, global);

            Assert.IsTrue(collection.Count > 0, $"UI-Element({typeof(T).Name}) not found using selector: {by}");

            return collection[0];
        }

        /// <summary>
        /// Shortcut for this.Find<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public T Find<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Find<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public Element Find(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Find<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public Element Find(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Find<Element>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Has only one Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.FindAll<T>(by, timeoutMS, global).Count == 1;
        }

        /// <summary>
        /// Shortcut for this.HasOne<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.HasOne<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.HasOne<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.HasOne<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.HasOne<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.HasOne<Element>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Has one or more Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.FindAll<T>(by, timeoutMS, global).Count >= 1;
        }

        /// <summary>
        /// Shortcut for this.Has<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Has<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Has<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Has<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Has<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Has<Element>(name, timeoutMS, global);
        }

        /// <summary>
        /// Finds all Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            var driver = global ? this.Root : this.WindowsDriver;
            Assert.IsNotNull(driver, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindHelper.FindAll<T, WindowsElement>(
                () =>
                {
                    if (by.GetIsAccessibilityId())
                    {
                        var elements = driver.FindElementsByAccessibilityId(by.GetAccessibilityId());
                        return elements;
                    }
                    else
                    {
                        var elements = driver.FindElements(by.ToSeleniumBy());
                        return elements;
                    }
                },
                driver,
                timeoutMS);

            return foundElements ?? new ReadOnlyCollection<T>([]);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="name">The name to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.FindAll<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.FindAll<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.FindAll<Element>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Close the main window.
        /// </summary>
        public void CloseMainWindow()
        {
            if (MainWindow != null)
            {
                MainWindow.Close();
                MainWindow = null;
            }
        }

        /// <summary>
        /// Sends a combination of keys.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        public void SendKeys(params Key[] keys)
        {
            PerformAction(() =>
            {
                KeyboardHelper.SendKeys(keys);
            });
        }

        /// <summary>
        /// release the key (after the hold key and drag is completed.)
        /// </summary>
        /// <param name="key">The key release.</param>
        public void PressKey(Key key)
        {
            PerformAction(() =>
            {
                KeyboardHelper.PressKey(key);
            });
        }

        /// <summary>
        /// press and hold the specified key.
        /// </summary>
        /// <param name="key">The key to press and hold .</param>
        public void ReleaseKey(Key key)
        {
            PerformAction(() =>
            {
                KeyboardHelper.ReleaseKey(key);
            });
        }

        /// <summary>
        /// press and hold the specified key.
        /// </summary>
        /// <param name="key">The key to press and release .</param>
        public void SendKey(Key key, int msPreAction = 500, int msPostAction = 500)
        {
            PerformAction(
                () =>
                {
                    KeyboardHelper.SendKey(key);
                },
                msPreAction,
                msPostAction);
        }

        /// <summary>
        /// Sends a sequence of keys.
        /// </summary>
        /// <param name="keys">An array of keys to send.</param>
        public void SendKeySequence(params Key[] keys)
        {
            PerformAction(() =>
            {
                foreach (var key in keys)
                {
                    KeyboardHelper.SendKeys(key);
                }
            });
        }

        /// <summary>
        /// Gets the current position of the mouse cursor as a tuple.
        /// </summary>
        /// <returns>A tuple containing the X and Y coordinates of the cursor.</returns>
        public Tuple<int, int> GetMousePosition()
        {
            return MouseHelper.GetMousePosition();
        }

        /// <summary>
        /// Moves the mouse cursor to the specified screen coordinates.
        /// </summary>
        /// <param name="x">The new x-coordinate of the cursor.</param>
        /// <param name="y">The new y-coordinate of the cursor.</param
        public void MoveMouseTo(int x, int y, int msPreAction = 500, int msPostAction = 500)
        {
            PerformAction(
                () =>
                {
                    MouseHelper.MoveMouseTo(x, y);
                },
                msPreAction,
                msPostAction);
        }

        /// <summary>
        /// Performs a mouse action based on the specified action type.
        /// </summary>
        /// <param name="action">The mouse action to perform.</param>
        /// <param name="msPreAction">Pre-action delay in milliseconds.</param>
        /// <param name="msPostAction">Post-action delay in milliseconds.</param>
        public void PerformMouseAction(MouseActionType action, int msPreAction = 500, int msPostAction = 500)
        {
            PerformAction(
                () =>
                {
                    switch (action)
                    {
                        case MouseActionType.LeftClick:
                            MouseHelper.LeftClick();
                            break;
                        case MouseActionType.RightClick:
                            MouseHelper.RightClick();
                            break;
                        case MouseActionType.MiddleClick:
                            MouseHelper.MiddleClick();
                            break;
                        case MouseActionType.LeftDoubleClick:
                            MouseHelper.LeftDoubleClick();
                            break;
                        case MouseActionType.RightDoubleClick:
                            MouseHelper.RightDoubleClick();
                            break;
                        case MouseActionType.LeftDown:
                            MouseHelper.LeftDown();
                            break;
                        case MouseActionType.LeftUp:
                            MouseHelper.LeftUp();
                            break;
                        case MouseActionType.RightDown:
                            MouseHelper.RightDown();
                            break;
                        case MouseActionType.RightUp:
                            MouseHelper.RightUp();
                            break;
                        case MouseActionType.MiddleDown:
                            MouseHelper.MiddleDown();
                            break;
                        case MouseActionType.MiddleUp:
                            MouseHelper.MiddleUp();
                            break;
                        case MouseActionType.ScrollUp:
                            MouseHelper.ScrollUp();
                            break;
                        case MouseActionType.ScrollDown:
                            MouseHelper.ScrollDown();
                            break;
                        default:
                            throw new ArgumentException("Unsupported mouse action.", nameof(action));
                    }
                },
                msPreAction,
                msPostAction);
        }

        /// <summary>
        /// Attaches to an existing PowerToys module.
        /// </summary>
        /// <param name="module">The PowerToys module to attach to.</param>
        /// <param name="size">The window size to set. Default is no change to window size</param>
        /// <returns>The attached session.</returns>
        public Session Attach(PowerToysModule module, WindowSize size = WindowSize.UnSpecified)
        {
            string windowName = ModuleConfigData.Instance.GetModuleWindowName(module);
            return this.Attach(windowName, size);
        }

        /// <summary>
        /// Attaches to an existing exe by string window name.
        /// The session should be attached when a new app is started.
        /// </summary>
        /// <param name="windowName">The window name to attach to.</param>
        /// <param name="size">The window size to set. Default is no change to window size</param>
        /// <returns>The attached session.</returns>
        public Session Attach(string windowName, WindowSize size = WindowSize.UnSpecified)
        {
            this.IsElevated = null;
            this.MainWindowHandler = IntPtr.Zero;

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
                this.MainWindowHandler = matchingWindows[0].HWnd;
                this.IsElevated = matchingWindows[0].Title.StartsWith(WindowHelper.AdministratorPrefix);

                ApiHelper.SetForegroundWindow(this.MainWindowHandler);

                var hexWindowHandle = this.MainWindowHandler.ToInt64().ToString("x");

                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                this.WindowsDriver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), appCapabilities);

                this.windowHandlers.Add(this.MainWindowHandler);

                if (size != WindowSize.UnSpecified)
                {
                    WindowHelper.SetWindowSize(this.MainWindowHandler, size);
                }

                // Set MainWindow
                MainWindow = Find<Window>(matchingWindows[0].Title);
            }
            else
            {
                Assert.IsNotNull(this.Root, $"Failed to attach to the window '{windowName}'. Root driver is null");
            }

            Task.Delay(3000).Wait();
            return this;
        }

        /// <summary>
        /// Sets the main window size.
        /// </summary>
        /// <param name="size">WindowSize enum</param>
        public void SetMainWindowSize(WindowSize size)
        {
            if (this.MainWindowHandler == IntPtr.Zero)
            {
                // Attach to the scope & reset MainWindowHandler
                this.Attach(this.InitScope);
            }

            WindowHelper.SetWindowSize(this.MainWindowHandler, size);
        }

        /// <summary>
        /// Gets the main window center coordinates.
        /// </summary>
        /// <returns>(x, y)</returns>
        public (int CenterX, int CenterY) GetMainWindowCenter()
        {
            return WindowHelper.GetWindowCenter(this.MainWindowHandler);
        }

        /// <summary>
        /// Gets the main window center coordinates.
        /// </summary>
        /// <returns>(int Left, int Top, int Right, int Bottom)</returns>
        public (int Left, int Top, int Right, int Bottom) GetMainWindowRect()
        {
            return WindowHelper.GetWindowRect(this.MainWindowHandler);
        }

        /// <summary>
        /// Launches the specified executable with optional arguments and simulates a delay before and after execution.
        /// </summary>
        /// <param name="executablePath">The full path to the executable to launch.</param>
        /// <param name="arguments">Optional command-line arguments to pass to the executable.</param>
        /// <param name="msPreAction">The number of milliseconds to wait before launching the executable. Default is 0 ms.</param>
        /// <param name="msPostAction">The number of milliseconds to wait after launching the executable. Default is 2000 ms.</param>
        public void StartExe(string executablePath, string arguments = "", int msPreAction = 0, int msPostAction = 2000)
        {
            PerformAction(
                () =>
                {
                    StartExeInternal(executablePath, arguments);
                },
                msPreAction,
                msPostAction);
        }

        private void StartExeInternal(string executablePath, string arguments = "")
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = true,
            };
            Process.Start(processInfo);
        }

        /// <summary>
        /// Terminates all running processes that match the specified process name.
        /// Waits for each process to exit after sending the kill signal.
        /// </summary>
        /// <param name="processName">The name of the process to terminate (without extension, e.g., "notepad").</param>
        public void KillAllProcessesByName(string processName)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        /// <summary>
        /// Simulates a manual operation on the element.
        /// </summary>
        /// <param name="action">The action to perform on the element.</param>
        /// <param name="msPreAction">The number of milliseconds to wait before the action. Default value is 500 ms</param>
        /// <param name="msPostAction">The number of milliseconds to wait after the action. Default value is 500 ms</param>
        protected void PerformAction(Action<Actions, WindowsDriver<WindowsElement>> action, int msPreAction = 500, int msPostAction = 500)
        {
            if (msPreAction > 0)
            {
                Task.Delay(msPreAction).Wait();
            }

            var windowsDriver = this.WindowsDriver;
            Actions actions = new Actions(this.WindowsDriver);
            action(actions, windowsDriver);

            if (msPostAction > 0)
            {
                Task.Delay(msPostAction).Wait();
            }
        }

        /// <summary>
        /// Simulates a manual operation on the element.
        /// </summary>
        /// <param name="action">The action to perform on the element.</param>
        /// <param name="msPreAction">The number of milliseconds to wait before the action. Default value is 500 ms</param>
        /// <param name="msPostAction">The number of milliseconds to wait after the action. Default value is 500 ms</param>
        protected void PerformAction(Action action, int msPreAction = 500, int msPostAction = 500)
        {
            if (msPreAction > 0)
            {
                Task.Delay(msPreAction).Wait();
            }

            action();

            if (msPostAction > 0)
            {
                Task.Delay(msPostAction).Wait();
            }
        }
    }
}
