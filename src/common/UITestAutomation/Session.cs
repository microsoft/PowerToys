// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides interfaces for interacting with UI elements.
    /// </summary>
    public class Session
    {
        public WindowsDriver<WindowsElement> Root { get; set; }

        private WindowsDriver<WindowsElement> WindowsDriver { get; set; }

        private const string AdministratorPrefix = "Administrator: ";

        private List<IntPtr> windowHandlers = new List<IntPtr>();

        private Window? MainWindow { get; set; }

        /// <summary>
        /// Gets Main Window Handler
        /// </summary>
        public IntPtr MainWindowHandler { get; private set; }

        /// <summary>
        /// Gets the RunAsAdmin flag.
        /// If true, the session is running as admin.
        /// If false, the session is not running as admin.
        /// If null, no information is available.
        /// </summary>
        public bool? IsElevated { get; private set; }

        public Session(WindowsDriver<WindowsElement> root, WindowsDriver<WindowsElement> windowsDriver, PowerToysModule scope, WindowSize size)
        {
            this.MainWindowHandler = IntPtr.Zero;
            this.Root = root;
            this.WindowsDriver = windowsDriver;

            // Attach to the scope & reset MainWindowHandler
            this.Attach(scope, size);
        }

        /// <summary>
        /// Cleans up the Session Exe.
        /// </summary>
        public void Cleanup()
        {
            /*
            foreach (var windowHandle in this.windowHandlers)
            {
                if (windowHandle == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var process = Process.GetProcessById((int)windowHandle);
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                catch
                {
                }
            }
            */

            windowHandlers.Clear();
        }

        /// <summary>
        /// Finds an Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public T Find<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsDriver, $"WindowsElement is null in method Find<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");

            // leverage findAll to filter out mismatched elements
            var collection = this.FindAll<T>(by, timeoutMS);

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
        public T Find<T>(string name, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.Find<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public Element Find(By by, int timeoutMS = 5000)
        {
            return this.Find<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Find<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        public Element Find(string name, int timeoutMS = 5000)
        {
            return this.Find<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Has only one Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.FindAll<T>(by, timeoutMS).Count == 1;
        }

        /// <summary>
        /// Shortcut for this.HasOne<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne(By by, int timeoutMS = 5000)
        {
            return this.HasOne<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.HasOne<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne<T>(string name, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.HasOne<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.HasOne<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element, otherwise false.</returns>
        public bool HasOne(string name, int timeoutMS = 5000)
        {
            return this.HasOne<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Has one or more Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.FindAll<T>(by, timeoutMS).Count >= 1;
        }

        /// <summary>
        /// Shortcut for this.Has<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has(By by, int timeoutMS = 5000)
        {
            return this.Has<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Has<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has<T>(string name, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.Has<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Shortcut for this.Has<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element, otherwise false.</returns>
        public bool Has(string name, int timeoutMS = 5000)
        {
            return this.Has<Element>(name, timeoutMS);
        }

        /// <summary>
        /// Finds all Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 5000)
            where T : Element, new()
        {
            Assert.IsNotNull(this.WindowsDriver, $"WindowsElement is null in method FindAll<{typeof(T).Name}> with parameters: by = {by}, timeoutMS = {timeoutMS}");
            var foundElements = FindHelper.FindAll<T, WindowsElement>(
                () =>
                {
                    if (by.GetIsAccessibilityId())
                    {
                        var elements = this.WindowsDriver.FindElementsByAccessibilityId(by.GetAccessibilityId());
                        return elements;
                    }
                    else
                    {
                        var elements = this.WindowsDriver.FindElements(by.ToSeleniumBy());
                        return elements;
                    }
                },
                this.WindowsDriver,
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
        public ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 5000)
            where T : Element, new()
        {
            return this.FindAll<T>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 5000)
        {
            return this.FindAll<Element>(by, timeoutMS);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        public ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 5000)
        {
            return this.FindAll<Element>(By.Name(name), timeoutMS);
        }

        /// <summary>
        /// Sets the main window size.
        /// </summary>
        /// <param name="size">WindowSize enum</param>
        public void SetMainWindowSize(WindowSize size)
        {
            if (size == WindowSize.UnSpecified)
            {
                return;
            }

            int width = 0, height = 0;

            switch (size)
            {
                case WindowSize.Small:
                    width = 640;
                    height = 480;
                    break;
                case WindowSize.Small_Vertical:
                    width = 480;
                    height = 640;
                    break;
                case WindowSize.Medium:
                    width = 1024;
                    height = 768;
                    break;
                case WindowSize.Medium_Vertical:
                    width = 768;
                    height = 1024;
                    break;
                case WindowSize.Large:
                    width = 1920;
                    height = 1080;
                    break;
                case WindowSize.Large_Vertical:
                    width = 1080;
                    height = 1920;
                    break;
            }

            if (width > 0 && height > 0)
            {
                this.SetMainWindowSize(width, height);
            }
        }

        /// <summary>
        /// Sets the main window size based on Width and Height.
        /// </summary>
        /// <param name="width">the width in pixel</param>
        /// <param name="height">the height in pixel</param>
        public void SetMainWindowSize(int width, int height)
        {
            if (this.MainWindowHandler == IntPtr.Zero
                || width <= 0
                || height <= 0)
            {
                return;
            }

            ApiHelper.SetWindowPos(this.MainWindowHandler, IntPtr.Zero, 0, 0, width, height, ApiHelper.SetWindowPosNoZorder | ApiHelper.SetWindowPosShowWindow);

            // Wait for 1000ms after resize
            Task.Delay(1000).Wait();
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
        /// Retrieves the color of the pixel at the specified screen coordinates.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = ApiHelper.GetDC(IntPtr.Zero);
            uint pixel = ApiHelper.GetPixel(hdc, x, y);
            _ = ApiHelper.ReleaseDC(IntPtr.Zero, hdc);

            int r = (int)(pixel & 0x000000FF);
            int g = (int)((pixel & 0x0000FF00) >> 8);
            int b = (int)((pixel & 0x00FF0000) >> 16);

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Retrieves the color of the pixel at the specified screen coordinates as a string.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public string GetPixelColorString(int x, int y)
        {
            Color color = this.GetPixelColor(x, y);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Gets the size of the display.
        /// </summary>
        /// <returns>
        /// A tuple containing the width and height of the display.
        /// </returns
        public Tuple<int, int> GetDisplaySize()
        {
            IntPtr hdc = ApiHelper.GetDC(IntPtr.Zero);
            int screenWidth = ApiHelper.GetDeviceCaps(hdc, ApiHelper.DESKTOPHORZRES);
            int screenHeight = ApiHelper.GetDeviceCaps(hdc, ApiHelper.DESKTOPVERTRES);
            _ = ApiHelper.ReleaseDC(IntPtr.Zero, hdc);

            return Tuple.Create(screenWidth, screenHeight);
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
        public void MoveMouseTo(int x, int y)
        {
            PerformAction(() =>
         {
             MouseHelper.MoveMouseTo(x, y);
         });
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
                var matchingWindows = ApiHelper.FindDesktopWindowHandler([windowName, AdministratorPrefix + windowName]);
                if (matchingWindows.Count == 0 || matchingWindows[0].HWnd == IntPtr.Zero)
                {
                    Assert.Fail($"Failed to attach. Window '{windowName}' not found");
                }

                // pick one from matching windows
                this.MainWindowHandler = matchingWindows[0].HWnd;
                this.IsElevated = matchingWindows[0].Title.StartsWith(AdministratorPrefix);

                ApiHelper.SetForegroundWindow(this.MainWindowHandler);

                var hexWindowHandle = this.MainWindowHandler.ToInt64().ToString("x");

                var appCapabilities = new AppiumOptions();
                appCapabilities.AddAdditionalCapability("appTopLevelWindow", hexWindowHandle);
                appCapabilities.AddAdditionalCapability("deviceName", "WindowsPC");
                this.WindowsDriver = new WindowsDriver<WindowsElement>(new Uri(ModuleConfigData.Instance.GetWindowsApplicationDriverUrl()), appCapabilities);

                this.windowHandlers.Add(this.MainWindowHandler);

                if (size != WindowSize.UnSpecified)
                {
                    this.SetMainWindowSize(size);
                }

                // Set MainWindow
                MainWindow = Find<Window>(matchingWindows[0].Title);
            }
            else
            {
                Assert.IsNotNull(this.Root, $"Failed to attach to the window '{windowName}'. Root driver is null");
            }

            return this;
        }

        private static class ApiHelper
        {
            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            public const uint SetWindowPosNoMove = 0x0002;
            public const uint SetWindowPosNoZorder = 0x0004;
            public const uint SetWindowPosShowWindow = 0x0040;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

            // Delegate for the EnumWindows callback function
            private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            // P/Invoke declaration for EnumWindows
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            // P/Invoke declaration for GetWindowTextLength
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int GetWindowTextLength(IntPtr hWnd);

            // P/Invoke declaration for GetWindowText
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("gdi32.dll")]
            public static extern uint GetPixel(IntPtr hdc, int x, int y);

            [DllImport("gdi32.dll")]
            public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

            public const int DESKTOPHORZRES = 118;
            public const int DESKTOPVERTRES = 117;

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            public static List<(IntPtr HWnd, string Title)> FindDesktopWindowHandler(string[] matchingWindowsTitles)
            {
                var windows = new List<(IntPtr HWnd, string Title)>();

                _ = EnumWindows(
                    (hWnd, lParam) =>
                    {
                        int length = GetWindowTextLength(hWnd);
                        if (length > 0)
                        {
                            var builder = new StringBuilder(length + 1);
                            _ = GetWindowText(hWnd, builder, builder.Capacity);

                            var title = builder.ToString();
                            if (matchingWindowsTitles.Contains(title))
                            {
                                windows.Add((hWnd, title));
                            }
                        }

                        return true; // Continue enumeration
                    },
                    IntPtr.Zero);

                return windows;
            }
        }

        public void StarteExe(string executablePath, string arguments = "", int msPreAction = 0, int msPostAction = 2000)
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
