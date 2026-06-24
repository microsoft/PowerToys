// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Base class that should be inherited by all Test Classes.
    /// </summary>
    [TestClass]
    public class UITestBase : IDisposable
    {
        public required TestContext TestContext { get; set; }

        public required Session Session { get; set; }

        /// <summary>
        /// Gets a value indicating whether the tests are running in a CI/CD pipeline.
        /// </summary>
        public bool IsInPipeline { get; }

        public string? ScreenshotDirectory { get; set; }

        public string? RecordingDirectory { get; set; }

        public static MonitorInfoData.ParamsWrapper MonitorInfoData { get; set; } = new MonitorInfoData.ParamsWrapper() { Monitors = new List<MonitorInfoData.MonitorInfoDataWrapper>() };

        private readonly PowerToysModule scope;
        private readonly WindowSize size;
        private readonly string[]? commandLineArgs;
        private SessionHelper? sessionHelper;
        private System.Threading.Timer? screenshotTimer;
        private ScreenRecording? screenRecording;

        public UITestBase(PowerToysModule scope = PowerToysModule.PowerToysSettings, WindowSize size = WindowSize.UnSpecified, string[]? commandLineArgs = null)
        {
            this.IsInPipeline = EnvironmentConfig.IsInPipeline;
            Console.WriteLine($"Running tests on platform: {EnvironmentConfig.Platform}");
            if (IsInPipeline)
            {
                NativeMethods.ChangeDisplayResolution(1920, 1080);
                NativeMethods.GetMonitorInfo();

                // Escape Popups before starting
                System.Windows.Forms.SendKeys.SendWait("{ESC}");
            }

            this.scope = scope;
            this.size = size;
            this.commandLineArgs = commandLineArgs;
        }

        /// <summary>
        /// Initializes the test.
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            KeyboardHelper.SendKeys(Key.Win, Key.M);
            CloseOtherApplications();
            if (IsInPipeline)
            {
                string baseDirectory = this.TestContext.TestResultsDirectory ?? string.Empty;
                ScreenshotDirectory = Path.Combine(baseDirectory, "UITestScreenshots_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(ScreenshotDirectory);

                RecordingDirectory = Path.Combine(baseDirectory, "UITestRecordings_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(RecordingDirectory);

                // Take screenshot every 1 second
                screenshotTimer = new System.Threading.Timer(ScreenCapture.TimerCallback, ScreenshotDirectory, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));

                // Start screen recording (requires FFmpeg)
                try
                {
                    screenRecording = new ScreenRecording(RecordingDirectory);
                    if (screenRecording.IsAvailable)
                    {
                        _ = screenRecording.StartRecordingAsync();
                    }
                    else
                    {
                        screenRecording = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start screen recording: {ex.Message}");
                    screenRecording = null;
                }

                // Escape Popups before starting
                System.Windows.Forms.SendKeys.SendWait("{ESC}");
            }

            this.sessionHelper = new SessionHelper(scope, commandLineArgs).Init();
            this.Session = new Session(this.sessionHelper.GetRoot(), this.sessionHelper.GetDriver(), scope, size);
        }

        /// <summary>
        /// Cleanups the test.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            if (IsInPipeline)
            {
                screenshotTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                // Stop screen recording
                if (screenRecording != null)
                {
                    try
                    {
                        screenRecording.StopRecordingAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to stop screen recording: {ex.Message}");
                    }
                }

                if (TestContext.CurrentTestOutcome is UnitTestOutcome.Failed
                    or UnitTestOutcome.Error
                    or UnitTestOutcome.Unknown)
                {
                    Task.Delay(1000).Wait();
                    AddScreenShotsToTestResultsDirectory();
                    AddRecordingsToTestResultsDirectory();
                    AddLogFilesToTestResultsDirectory();
                }
                else
                {
                    // Clean up recording if test passed
                    CleanupRecordingDirectory();
                }

                Dispose();
            }

            this.Session.Cleanup();
            this.sessionHelper!.Cleanup();
        }

        public void Dispose()
        {
            screenshotTimer?.Dispose();
            screenRecording?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finds an element by selector.
        /// Shortcut for this.Session.Find<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T Find<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.Find<T>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Find<Element>(name, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T Find<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.Find<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Find<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element Find(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.Find(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Find<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element Find(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.Find(name, timeoutMS, global);
        }

        /// <summary>
        /// Has only one Element or its derived class by selector.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element; otherwise, false.</returns>
        public bool HasOne<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.FindAll<T>(by, timeoutMS, global).Count == 1;
        }

        /// <summary>
        /// Shortcut for this.Session.HasOne<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element; otherwise, false.</returns>
        public bool HasOne(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.HasOne<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.HasOne<T>(name, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element; otherwise, false.</returns>
        public bool HasOne<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.HasOne<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.HasOne<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if only has one element; otherwise, false.</returns>
        public bool HasOne(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.HasOne<Element>(name, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Has<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element; otherwise, false.</returns>
        public bool Has<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.FindAll<T>(by, timeoutMS, global).Count >= 1;
        }

        /// <summary>
        /// Shortcut for this.Session.Has<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element; otherwise, false.</returns>
        public bool Has(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.Has<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Has<T>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element; otherwise, false.</returns>
        public bool Has<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.Has<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Shortcut for this.Session.Has<Element>(name, timeoutMS)
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>True if  has one or more element; otherwise, false.</returns>
        public bool Has(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.Has<Element>(name, timeoutMS, global);
        }

        /// <summary>
        /// Finds an element using partial name matching (contains).
        /// Useful for finding windows with variable titles like "filename.txt - Notepad" or "filename - Notepad".
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="partialName">Part of the name to search for.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T FindByPartialName<T>(string partialName, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return Session.Find<T>(By.XPath($"//*[contains(@Name, '{partialName}')]"), timeoutMS, global);
        }

        /// <summary>
        /// Finds an element using partial name matching (contains).
        /// </summary>
        /// <param name="partialName">Part of the name to search for.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element FindByPartialName(string partialName, int timeoutMS = 5000, bool global = false)
        {
            return FindByPartialName<Element>(partialName, timeoutMS, global);
        }

        /// <summary>
        /// Base method for finding elements by selector and filtering by name pattern.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="selector">The selector to find initial candidates.</param>
        /// <param name="namePattern">Pattern to match against the Name attribute. Supports regex patterns.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <param name="errorMessage">Custom error message when no element is found.</param>
        /// <returns>The found element.</returns>
        private T FindByNamePattern<T>(By selector, string namePattern, int timeoutMS = 5000, bool global = false, string? errorMessage = null)
            where T : Element, new()
        {
            var elements = Session.FindAll<T>(selector, timeoutMS, global);
            var regex = new Regex(namePattern, RegexOptions.IgnoreCase);

            foreach (var element in elements)
            {
                var name = element.GetAttribute("Name");
                if (!string.IsNullOrEmpty(name) && regex.IsMatch(name))
                {
                    return element;
                }
            }

            throw new NoSuchElementException(errorMessage ?? $"No element found matching pattern: {namePattern}");
        }

        /// <summary>
        /// Finds an element using regular expression pattern matching.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="pattern">Regular expression pattern to match against the Name attribute.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T FindByPattern<T>(string pattern, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return FindByNamePattern<T>(By.XPath("//*[@Name]"), pattern, timeoutMS, global, $"No element found matching pattern: {pattern}");
        }

        /// <summary>
        /// Finds an element using regular expression pattern matching.
        /// </summary>
        /// <param name="pattern">Regular expression pattern to match against the Name attribute.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element FindByPattern(string pattern, int timeoutMS = 5000, bool global = false)
        {
            return FindByPattern<Element>(pattern, timeoutMS, global);
        }

        /// <summary>
        /// Finds an element by ClassName only.
        /// Returns the first element found with the specified ClassName.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="className">The ClassName to search for (e.g., "Notepad", "CabinetWClass").</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T FindByClassName<T>(string className, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return Session.Find<T>(By.ClassName(className), timeoutMS, global);
        }

        /// <summary>
        /// Finds an element by ClassName only.
        /// Returns the first element found with the specified ClassName.
        /// </summary>
        /// <param name="className">The ClassName to search for (e.g., "Notepad", "CabinetWClass").</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element FindByClassName(string className, int timeoutMS = 5000, bool global = false)
        {
            return FindByClassName<Element>(className, timeoutMS, global);
        }

        /// <summary>
        /// Finds an element by ClassName and matches its Name attribute using regex pattern matching.
        /// </summary>
        /// <typeparam name="T">The class of the element, should be Element or its derived class.</typeparam>
        /// <param name="className">The ClassName to search for (e.g., "Notepad", "CabinetWClass").</param>
        /// <param name="namePattern">Pattern to match against the Name attribute. Supports regex patterns.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected T FindByClassNameAndNamePattern<T>(string className, string namePattern, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return FindByNamePattern<T>(By.ClassName(className), namePattern, timeoutMS, global, $"No element with ClassName '{className}' found matching name pattern: {namePattern}");
        }

        /// <summary>
        /// Finds an element by ClassName and matches its Name attribute using regex pattern matching.
        /// </summary>
        /// <param name="className">The ClassName to search for (e.g., "Notepad", "CabinetWClass").</param>
        /// <param name="namePattern">Pattern to match against the Name attribute. Supports regex patterns.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found element.</returns>
        protected Element FindByClassNameAndNamePattern(string className, string namePattern, int timeoutMS = 5000, bool global = false)
        {
            return FindByClassNameAndNamePattern<Element>(className, namePattern, timeoutMS, global);
        }

        /// <summary>
        /// Finds a Notepad window regardless of whether the file extension is shown in the title.
        /// Handles both "filename.txt - Notepad" and "filename - Notepad" formats.
        /// Uses ClassName to efficiently find Notepad windows first, then matches the filename.
        /// </summary>
        /// <param name="baseFileName">The base filename without extension (e.g., "test" for "test.txt").</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found Notepad window element.</returns>
        protected Element FindNotepadWindow(string baseFileName, int timeoutMS = 5000, bool global = false)
        {
            string pattern = $@"^{Regex.Escape(baseFileName)}(\.\w+)?(\s*-\s*|\s+)Notepad$";
            return FindByClassNameAndNamePattern("Notepad", pattern, timeoutMS, global);
        }

        /// <summary>
        /// Finds an Explorer window regardless of the folder or file name display format.
        /// Handles various Explorer window title formats like "FolderName", "FileName", "FolderName - File Explorer", etc.
        /// Uses ClassName to efficiently find Explorer windows first, then matches the folder or file name.
        /// </summary>
        /// <param name="folderName">The folder or file name to search for (e.g., "Documents", "Desktop", "test.txt").</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found Explorer window element.</returns>
        protected Element FindExplorerWindow(string folderName, int timeoutMS = 5000, bool global = false)
        {
            string pattern = $@"^{Regex.Escape(folderName)}(\s*-\s*(File\s+Explorer|Windows\s+Explorer))?$";
            return FindByClassNameAndNamePattern("CabinetWClass", pattern, timeoutMS, global);
        }

        /// <summary>
        /// Finds an Explorer window by partial folder path.
        /// Useful when the full path might be displayed in the title.
        /// </summary>
        /// <param name="partialPath">Part of the folder path to search for.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>The found Explorer window element.</returns>
        protected Element FindExplorerByPartialPath(string partialPath, int timeoutMS = 5000, bool global = false)
        {
            return FindByPartialName(partialPath, timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<T>(by, timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.FindAll<T>(by, timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <typeparam name="T">The class of the elements, should be Element or its derived class.</typeparam>
        /// <param name="name">The name of the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<T> FindAll<T>(string name, int timeoutMS = 5000, bool global = false)
            where T : Element, new()
        {
            return this.Session.FindAll<T>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<Element>(by, timeoutMS)
        /// </summary>
        /// <param name="by">The selector to find the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<Element> FindAll(By by, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.FindAll<Element>(by, timeoutMS, global);
        }

        /// <summary>
        /// Finds all elements by selector.
        /// Shortcut for this.Session.FindAll<Element>(By.Name(name), timeoutMS)
        /// </summary>
        /// <param name="name">The name of the elements.</param>
        /// <param name="timeoutMS">The timeout in milliseconds (default is 5000).</param>
        /// <returns>A read-only collection of the found elements.</returns>
        protected ReadOnlyCollection<Element> FindAll(string name, int timeoutMS = 5000, bool global = false)
        {
            return this.Session.FindAll<Element>(By.Name(name), timeoutMS, global);
        }

        /// <summary>
        /// Scrolls the page
        /// </summary>
        /// <param name="scrollCount">The number of scroll attempts.</param>
        /// <param name="direction">The direction to scroll.</param>
        /// <param name="msPreAction">Pre-action delay in milliseconds.</param>
        /// <param name="msPostAction">Post-action delay in milliseconds.</param>
        public void Scroll(int scrollCount = 5, string direction = "Up", int msPreAction = 500, int msPostAction = 500)
        {
            MouseActionType mouseAction = direction == "Up" ? MouseActionType.ScrollUp : MouseActionType.ScrollDown;
            for (int i = 0; i < scrollCount; i++)
            {
                Session.PerformMouseAction(mouseAction, msPreAction, msPostAction); // Ensure settings are visible
            }
        }

        /// <summary>
        /// Captures the last screenshot when the test fails.
        /// </summary>
        protected void CaptureLastScreenshot()
        {
            // Implement your screenshot capture logic here
            // For example, save a screenshot to a file and return the file path
            string screenshotPath = Path.Combine(this.TestContext.TestResultsDirectory ?? string.Empty, "last_screenshot.png");

            this.Session.Root.GetScreenshot().SaveAsFile(screenshotPath, ScreenshotImageFormat.Png);

            // Save screenshot to screenshotPath & upload to test attachment
            this.TestContext.AddResultFile(screenshotPath);
        }

        /// <summary>
        /// Retrieves the color of the pixel at the specified screen coordinates.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public Color GetPixelColor(int x, int y)
        {
            return WindowHelper.GetPixelColor(x, y);
        }

        /// <summary>
        /// Retrieves the color of the pixel at the specified screen coordinates as a string.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public string GetPixelColorString(int x, int y)
        {
            return WindowHelper.GetPixelColorString(x, y);
        }

        /// <summary>
        /// Gets the size of the display.
        /// </summary>
        /// <returns>
        /// A tuple containing the width and height of the display.
        /// </returns
        public Tuple<int, int> GetDisplaySize()
        {
            return WindowHelper.GetDisplaySize();
        }

        /// <summary>
        /// Sends a combination of keys.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        public void SendKeys(params Key[] keys)
        {
            this.Session.SendKeys(keys);
        }

        /// <summary>
        /// Sends a sequence of keys.
        /// </summary>
        /// <param name="keys">An array of keys to send.</param>
        public void SendKeySequence(params Key[] keys)
        {
            this.Session.SendKeySequence(keys);
        }

        /// <summary>
        /// Gets the current position of the mouse cursor as a tuple.
        /// </summary>
        /// <returns>A tuple containing the X and Y coordinates of the cursor.</returns>
        public Tuple<int, int> GetMousePosition()
        {
            return this.Session.GetMousePosition();
        }

        /// <summary>
        /// Gets the screen center coordinates.
        /// </summary>
        /// <returns>(x, y)</returns>
        public (int CenterX, int CenterY) GetScreenCenter()
        {
            return WindowHelper.GetScreenCenter();
        }

        public bool IsWindowOpen(string windowName)
        {
            return WindowHelper.IsWindowOpen(windowName);
        }

        /// <summary>
        /// Moves the mouse cursor to the specified screen coordinates.
        /// </summary>
        /// <param name="x">The new x-coordinate of the cursor.</param>
        /// <param name="y">The new y-coordinate of the cursor.</param
        public void MoveMouseTo(int x, int y)
        {
            this.Session.MoveMouseTo(x, y);
        }

        protected void AddScreenShotsToTestResultsDirectory()
        {
            if (ScreenshotDirectory != null)
            {
                foreach (string file in Directory.GetFiles(ScreenshotDirectory))
                {
                    this.TestContext.AddResultFile(file);
                }
            }
        }

        /// <summary>
        /// Adds screen recordings to test results directory when test fails.
        /// </summary>
        protected void AddRecordingsToTestResultsDirectory()
        {
            if (RecordingDirectory != null && Directory.Exists(RecordingDirectory))
            {
                // Add video files (MP4)
                var videoFiles = Directory.GetFiles(RecordingDirectory, "*.mp4");
                foreach (string file in videoFiles)
                {
                    this.TestContext.AddResultFile(file);
                    var fileInfo = new FileInfo(file);
                    Console.WriteLine($"Added video recording: {Path.GetFileName(file)} ({fileInfo.Length / 1024 / 1024:F1} MB)");
                }

                if (videoFiles.Length == 0)
                {
                    Console.WriteLine("No video recording available (FFmpeg not found). Screenshots are still captured.");
                }
            }
        }

        /// <summary>
        /// Cleans up recording directory when test passes.
        /// </summary>
        private void CleanupRecordingDirectory()
        {
            if (RecordingDirectory != null && Directory.Exists(RecordingDirectory))
            {
                try
                {
                    Directory.Delete(RecordingDirectory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to cleanup recording directory: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copies PowerToys log files to test results directory when test fails.
        /// Renames files to include the directory structure after \PowerToys.
        /// </summary>
        protected void AddLogFilesToTestResultsDirectory()
        {
            try
            {
                var localAppDataLow = Path.Combine(
                    Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty,
                    "AppData",
                    "LocalLow",
                    "Microsoft",
                    "PowerToys");

                if (Directory.Exists(localAppDataLow))
                {
                    CopyLogFilesFromDirectory(localAppDataLow, string.Empty);
                }

                var localAppData = Path.Combine(
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty,
                    "Microsoft",
                    "PowerToys");

                if (Directory.Exists(localAppData))
                {
                    CopyLogFilesFromDirectory(localAppData, string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Don't fail the test if log file copying fails
                Console.WriteLine($"Failed to copy log files: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively copies log files from a directory and renames them with directory structure.
        /// </summary>
        /// <param name="sourceDir">Source directory to copy from</param>
        /// <param name="relativePath">Relative path from PowerToys folder</param>
        private void CopyLogFilesFromDirectory(string sourceDir, string relativePath)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            // Process log files in current directory
            var logFiles = Directory.GetFiles(sourceDir, "*.log");
            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(logFile);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);

                    // Create new filename with directory structure
                    var directoryPart = string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath.Replace("\\", "-") + "-";
                    var newFileName = $"{directoryPart}{fileNameWithoutExt}{extension}";

                    // Copy file to test results directory with new name
                    var testResultsDir = TestContext.TestResultsDirectory ?? Path.GetTempPath();
                    var destinationPath = Path.Combine(testResultsDir, newFileName);

                    File.Copy(logFile, destinationPath, true);
                    TestContext.AddResultFile(destinationPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy log file {logFile}: {ex.Message}");
                }
            }

            // Recursively process subdirectories
            var subdirectories = Directory.GetDirectories(sourceDir);
            foreach (var subdir in subdirectories)
            {
                var dirName = Path.GetFileName(subdir);
                var newRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);
                CopyLogFilesFromDirectory(subdir, newRelativePath);
            }
        }

        /// <summary>
        /// Restart scope exe.
        /// </summary>
        public Session RestartScopeExe(string? enableModules = null)
        {
            this.sessionHelper!.RestartScopeExe(enableModules);
            this.Session = new Session(this.sessionHelper.GetRoot(), this.sessionHelper.GetDriver(), this.scope, this.size);
            return Session;
        }

        /// <summary>
        /// Restart scope exe.
        /// </summary>
        public void ExitScopeExe()
        {
            this.sessionHelper!.ExitScopeExe();
            return;
        }

        private void CloseOtherApplications()
        {
            // Close other applications
            var processNamesToClose = new List<string>
            {
                "PowerToys",
                "PowerToys.Settings",
                "PowerToys.FancyZonesEditor",
            };
            foreach (var processName in processNamesToClose)
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
        }

        public class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            public struct DISPLAY_DEVICE
            {
                public int cb;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string DeviceName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceString;
                public int StateFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceID;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceKey;
            }

            [DllImport("user32.dll")]
            private static extern int EnumDisplaySettings(IntPtr deviceName, int modeNum, ref DEVMODE devMode);

            [DllImport("user32.dll")]
            private static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

            [DllImport("user32.dll", CharSet = CharSet.Ansi)]
            private static extern bool EnumDisplayDevices(IntPtr lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, int dwFlags);

            [DllImport("user32.dll")]
            private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

            [DllImport("user32.dll", CharSet = CharSet.Ansi)]
            private static extern int ChangeDisplaySettingsEx(IntPtr lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

            private const int DM_PELSWIDTH = 0x80000;
            private const int DM_PELSHEIGHT = 0x100000;

            public const int ENUM_CURRENT_SETTINGS = -1;
            public const int CDS_TEST = 0x00000002;
            public const int CDS_UPDATEREGISTRY = 0x01;
            public const int DISP_CHANGE_SUCCESSFUL = 0;
            public const int DISP_CHANGE_RESTART = 1;
            public const int DISP_CHANGE_FAILED = -1;

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVMODE
            {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string DmDeviceName;
                public short DmSpecVersion;
                public short DmDriverVersion;
                public short DmSize;
                public short DmDriverExtra;
                public int DmFields;
                public int DmPositionX;
                public int DmPositionY;
                public int DmDisplayOrientation;
                public int DmDisplayFixedOutput;
                public short DmColor;
                public short DmDuplex;
                public short DmYResolution;
                public short DmTTOption;
                public short DmCollate;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string DmFormName;
                public short DmLogPixels;
                public int DmBitsPerPel;
                public int DmPelsWidth;
                public int DmPelsHeight;
                public int DmDisplayFlags;
                public int DmDisplayFrequency;
                public int DmICMMethod;
                public int DmICMIntent;
                public int DmMediaType;
                public int DmDitherType;
                public int DmReserved1;
                public int DmReserved2;
                public int DmPanningWidth;
                public int DmPanningHeight;
            }

            public static void GetMonitorInfo()
            {
                int deviceIndex = 0;
                DISPLAY_DEVICE d = default(DISPLAY_DEVICE);
                d.cb = Marshal.SizeOf(d);

                Console.WriteLine("monitor list :");
                while (EnumDisplayDevices(IntPtr.Zero, deviceIndex, ref d, 0))
                {
                    Console.WriteLine($"monitor {deviceIndex + 1}:");
                    Console.WriteLine($"  name: {d.DeviceName}");
                    Console.WriteLine($"  string: {d.DeviceString}");
                    Console.WriteLine($"  ID: {d.DeviceID}");
                    Console.WriteLine($"  key: {d.DeviceKey}");
                    Console.WriteLine();

                    DEVMODE dm = default(DEVMODE);
                    dm.DmSize = (short)Marshal.SizeOf<DEVMODE>();
                    int modeNum = 0;
                    while (EnumDisplaySettings(d.DeviceName, modeNum, ref dm) > 0)
                    {
                        MonitorInfoData.Monitors.Add(new MonitorInfoData.MonitorInfoDataWrapper()
                        {
                            DeviceName = d.DeviceName,
                            DeviceString = d.DeviceString,
                            DeviceID = d.DeviceID,
                            DeviceKey = d.DeviceKey,
                            PelsWidth = dm.DmPelsWidth,
                            PelsHeight = dm.DmPelsHeight,
                            DisplayFrequency = dm.DmDisplayFrequency,
                        });
                        Console.WriteLine($"  mode {modeNum}: {dm.DmPelsWidth}x{dm.DmPelsHeight} @ {dm.DmDisplayFrequency}Hz");
                        modeNum++;
                    }

                    deviceIndex++;
                    d.cb = Marshal.SizeOf(d); // Reset the size for the next device
                }
            }

            public static void ChangeDisplayResolution(int PelsWidth, int PelsHeight)
            {
                Screen screen = Screen.PrimaryScreen!;
                if (screen.Bounds.Width == PelsWidth && screen.Bounds.Height == PelsHeight)
                {
                    return;
                }

                DEVMODE devMode = default(DEVMODE);
                devMode.DmDeviceName = new string(new char[32]);
                devMode.DmFormName = new string(new char[32]);
                devMode.DmSize = (short)Marshal.SizeOf<DEVMODE>();

                int modeNum = 0;
                while (EnumDisplaySettings(IntPtr.Zero, modeNum, ref devMode) > 0)
                {
                    Console.WriteLine($"Mode {modeNum}: {devMode.DmPelsWidth}x{devMode.DmPelsHeight} @ {devMode.DmDisplayFrequency}Hz");
                    modeNum++;
                }

                devMode.DmPelsWidth = PelsWidth;
                devMode.DmPelsHeight = PelsHeight;

                int result = NativeMethods.ChangeDisplaySettings(ref devMode, NativeMethods.CDS_TEST);

                if (result == DISP_CHANGE_SUCCESSFUL)
                {
                    result = ChangeDisplaySettings(ref devMode, CDS_UPDATEREGISTRY);
                    if (result == DISP_CHANGE_SUCCESSFUL)
                    {
                        Console.WriteLine($"Changing display resolution to {devMode.DmPelsWidth}x{devMode.DmPelsHeight}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to change display resolution. Error code: {result}");
                    }
                }
                else if (result == DISP_CHANGE_RESTART)
                {
                    Console.WriteLine($"Changing display resolution to {devMode.DmPelsWidth}x{devMode.DmPelsHeight} requires a restart");
                }
                else
                {
                    Console.WriteLine($"Failed to change display resolution. Error code: {result}");
                }
            }

            // Windows API for moving windows
            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

            private const uint SWPNOSIZE = 0x0001;
            private const uint SWPNOZORDER = 0x0004;

            public static void MoveWindow(Element window, int x, int y)
            {
                var windowHandle = IntPtr.Parse(window.GetAttribute("NativeWindowHandle") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                if (windowHandle != IntPtr.Zero)
                {
                    SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, SWPNOSIZE | SWPNOZORDER);
                    Task.Delay(500).Wait();
                }
            }
        }
    }
}
