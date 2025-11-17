// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    // Timeout constants for better maintainability
    private const int ExplorerOpenTimeoutSeconds = 15;
    private const int PeekWindowTimeoutSeconds = 15;
    private const int ExplorerLoadDelayMs = 3000;
    private const int ExplorerCheckIntervalMs = 1000;
    private const int PeekCheckIntervalMs = 1000;
    private const int PeekInitializeDelayMs = 3000;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 3000;
    private const int PinActionDelayMs = 500;

    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical)
    {
    }

    static PeekFilePreviewTests()
    {
        FixSettingsFileBeforeTests();
    }

    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private static void FixSettingsFileBeforeTests()
    {
        try
        {
            // Default Peek settings
            string peekSettingsContent = @"{
                  ""name"": ""Peek"",
                  ""version"": ""1.0"",
                  ""properties"": {
                    ""ActivationShortcut"": {
                      ""win"": false,
                      ""ctrl"": true,
                      ""alt"": false,
                      ""shift"": false,
                      ""code"": 32,
                      ""key"": ""Space""
                    },
                    ""AlwaysRunNotElevated"": {
                      ""value"": true
                    },
                    ""CloseAfterLosingFocus"": {
                      ""value"": false
                    },
                    ""ConfirmFileDelete"": {
                      ""value"": true
                    },
                    ""EnableSpaceToActivate"": {
                      ""value"": false
                    }
                  }
                }";

            // Update Peek module settings
            SettingsConfigHelper.UpdateModuleSettings(
                "Peek",
                peekSettingsContent,
                (settings) =>
                {
                    // Get or ensure properties section exists
                    Dictionary<string, object> properties;

                    if (settings.TryGetValue("properties", out var propertiesObj))
                    {
                        if (propertiesObj is Dictionary<string, object> dict)
                        {
                            properties = dict;
                        }
                        else if (propertiesObj is JsonElement jsonElem)
                        {
                            properties = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElem.GetRawText())
                                        ?? throw new InvalidOperationException("Failed to deserialize properties");
                        }
                        else
                        {
                            properties = new Dictionary<string, object>();
                        }
                    }
                    else
                    {
                        properties = new Dictionary<string, object>();
                    }

                    // Update the required properties
                    properties["ActivationShortcut"] = new Dictionary<string, object>
                    {
                        { "win", false },
                        { "ctrl", true },
                        { "alt", false },
                        { "shift", false },
                        { "code", 32 },
                        { "key", "Space" },
                    };

                    properties["EnableSpaceToActivate"] = new Dictionary<string, object>
                    {
                        { "value", false },
                    };

                    settings["properties"] = properties;
                });

            // Disable all modules except Peek in global settings
            SettingsConfigHelper.ConfigureGlobalModuleSettings("Peek");

            Debug.WriteLine("Successfully updated all settings - Peek shortcut configured and all modules except Peek disabled");
        }
        catch (Exception ex)
        {
            Assert.Fail($"ERROR in FixSettingsFileBeforeTests: {ex.Message}");
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Session.CloseMainWindow();
        SendKeys(Key.Win, Key.M);
    }

    [TestMethod("Peek.FilePreview.Folder")]
    [TestCategory("Preview files")]
    public void PeekFolderFilePreview()
    {
        string folderFullPath = Path.GetFullPath(@".\TestAssets");

        var peekWindow = OpenPeekWindow(folderFullPath);

        Assert.IsNotNull(peekWindow);

        Assert.IsNotNull(peekWindow.Find<TextBlock>("File Type: File folder", 500), "Folder preview should be loaded successfully");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test JPEG image preview
    /// </summary>
    [TestMethod("Peek.FilePreview.JPEGImage")]
    [TestCategory("Preview files")]
    public void PeekJPEGImagePreview()
    {
        string imagePath = Path.GetFullPath(@".\TestAssets\2.jpg");
        TestSingleFilePreview(imagePath, "2");
    }

    /// <summary>
    /// Test PDF document preview
    /// ToDo: need to open settings to enable PDF preview in Peek
    /// </summary>
    // [TestMethod("Peek.FilePreview.PDFDocument")]
    // [TestCategory("Preview files")]
    // public void PeekPDFDocumentPreview()
    // {
    //    string pdfPath = Path.GetFullPath(@".\TestAssets\3.pdf");
    //    TestSingleFilePreview(pdfPath, "3", 10000);
    // }

    /// <summary>
    /// Test QOI image preview
    /// </summary>
    [TestMethod("Peek.FilePreview.QOIImage")]
    [TestCategory("Preview files")]
    public void PeekQOIImagePreview()
    {
        string qoiPath = Path.GetFullPath(@".\TestAssets\4.qoi");
        TestSingleFilePreview(qoiPath, "4");
    }

    /// <summary>
    /// Test C++ source code preview
    /// </summary>
    [TestMethod("Peek.FilePreview.CPPSourceCode")]
    [TestCategory("Preview files")]
    public void PeekCPPSourceCodePreview()
    {
        string cppPath = Path.GetFullPath(@".\TestAssets\5.cpp");
        TestSingleFilePreview(cppPath, "5");
    }

    /// <summary>
    /// Test Markdown document preview
    /// </summary>
    [TestMethod("Peek.FilePreview.MarkdownDocument")]
    [TestCategory("Preview files")]
    public void PeekMarkdownDocumentPreview()
    {
        string markdownPath = Path.GetFullPath(@".\TestAssets\6.md");
        TestSingleFilePreview(markdownPath, "6");
    }

    /// <summary>
    /// Test ZIP archive preview
    /// </summary>
    [TestMethod("Peek.FilePreview.ZIPArchive")]
    [TestCategory("Preview files")]
    public void PeekZIPArchivePreview()
    {
        string zipPath = Path.GetFullPath(@".\TestAssets\7.zip");
        TestSingleFilePreview(zipPath, "7");
    }

    /// <summary>
    /// Test PNG image preview
    /// </summary>
    [TestMethod("Peek.FilePreview.PNGImage")]
    [TestCategory("Preview files")]
    public void PeekPNGImagePreview()
    {
        string pngPath = Path.GetFullPath(@".\TestAssets\8.png");
        TestSingleFilePreview(pngPath, "8");
    }

    /// <summary>
    /// Test window pinning functionality - pin window and switch between different sized images
    /// Verify the window stays at the same place and the same size
    /// </summary>
    [TestMethod("Peek.WindowPinning.PinAndSwitchImages")]
    [TestCategory("Window Pinning")]
    public void TestPinWindowAndSwitchImages()
    {
        // Use two different image files with different size
        string firstImagePath = Path.GetFullPath(@".\TestAssets\8.png");
        string secondImagePath = Path.GetFullPath(@".\TestAssets\2.jpg"); // Different format/size

        // Open first image
        var initialWindow = OpenPeekWindow(firstImagePath);

        var originalBounds = GetWindowBounds(initialWindow);

        // Move window to a custom position to test pin functionality
        NativeMethods.MoveWindow(initialWindow, originalBounds.X + 100, originalBounds.Y + 50);
        var movedBounds = GetWindowBounds(initialWindow);

        // Pin the window
        PinWindow();

        // Close current peek
        ClosePeekAndExplorer();

        // Open second image with different size
        var secondWindow = OpenPeekWindow(secondImagePath);
        var finalBounds = GetWindowBounds(secondWindow);

        // Verify window position and size remained the same as the moved position
        Assert.AreEqual(movedBounds.X, finalBounds.X, 5, "Window X position should remain the same when pinned");
        Assert.AreEqual(movedBounds.Y, finalBounds.Y, 5, "Window Y position should remain the same when pinned");
        Assert.AreEqual(movedBounds.Width, finalBounds.Width, 10, "Window width should remain the same when pinned");
        Assert.AreEqual(movedBounds.Height, finalBounds.Height, 10, "Window height should remain the same when pinned");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test window pinning persistence - pin window, close and reopen Peek
    /// Verify the new window is opened at the same place and the same size as before
    /// </summary>
    [TestMethod("Peek.WindowPinning.PinAndReopen")]
    [TestCategory("Window Pinning")]
    public void TestPinWindowAndReopen()
    {
        string imagePath = Path.GetFullPath(@".\TestAssets\8.png");

        // Open image and pin window
        var initialWindow = OpenPeekWindow(imagePath);
        var originalBounds = GetWindowBounds(initialWindow);

        // Move window to a custom position to test pin persistence
        NativeMethods.MoveWindow(initialWindow, originalBounds.X + 150, originalBounds.Y + 75);
        var movedBounds = GetWindowBounds(initialWindow);

        // Pin the window
        PinWindow();

        // Close peek
        ClosePeekAndExplorer();
        Thread.Sleep(1000); // Wait for window to close completely

        // Reopen the same image
        var reopenedWindow = OpenPeekWindow(imagePath);
        var finalBounds = GetWindowBounds(reopenedWindow);

        // Verify window position and size are restored to the moved position
        Assert.AreEqual(movedBounds.X, finalBounds.X, 5, "Window X position should be restored when pinned");
        Assert.AreEqual(movedBounds.Y, finalBounds.Y, 5, "Window Y position should be restored when pinned");
        Assert.AreEqual(movedBounds.Width, finalBounds.Width, 10, "Window width should be restored when pinned");
        Assert.AreEqual(movedBounds.Height, finalBounds.Height, 10, "Window height should be restored when pinned");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test window unpinning - unpin window and switch to different file
    /// Verify the window is moved to the default place
    /// </summary>
    [TestMethod("Peek.WindowPinning.UnpinAndSwitchFiles")]
    [TestCategory("Window Pinning")]
    public void TestUnpinWindowAndSwitchFiles()
    {
        string firstFilePath = Path.GetFullPath(@".\TestAssets\8.png");
        string secondFilePath = Path.GetFullPath(@".\TestAssets\2.jpg");

        // Open first file and pin window
        var pinnedWindow = OpenPeekWindow(firstFilePath);
        var originalBounds = GetWindowBounds(pinnedWindow);

        // Move window to a custom position
        NativeMethods.MoveWindow(pinnedWindow, originalBounds.X + 200, originalBounds.Y + 100);
        var movedBounds = GetWindowBounds(pinnedWindow);

        // Calculate the center point of the moved window
        var movedCenter = Session.GetMainWindowCenter();

        // Pin the window first
        PinWindow();

        // Unpin the window
        UnpinWindow();

        // Close current peek
        ClosePeekAndExplorer();

        // Open different file (different size)
        var unpinnedWindow = OpenPeekWindow(secondFilePath);
        var unpinnedBounds = GetWindowBounds(unpinnedWindow);

        // Calculate the center point of the unpinned window
        var unpinnedCenter = Session.GetMainWindowCenter();

        // Verify window size is different (since it's a different file type)
        bool sizeChanged = Math.Abs(movedBounds.Width - unpinnedBounds.Width) > 10 ||
                          Math.Abs(movedBounds.Height - unpinnedBounds.Height) > 10;

        // Verify window center moved to default position (should be different from moved center)
        bool centerChanged = Math.Abs(movedCenter.CenterX - unpinnedCenter.CenterX) > 50 ||
                            Math.Abs(movedCenter.CenterY - unpinnedCenter.CenterY) > 50;

        Assert.IsTrue(sizeChanged, "Window size should be different for different file types");
        Assert.IsTrue(centerChanged, "Window center should move to default position when unpinned");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test unpinned window behavior - unpin window, close and reopen Peek
    /// Verify the new window is opened on the default place
    /// </summary>
    [TestMethod("Peek.WindowPinning.UnpinAndReopen")]
    [TestCategory("Window Pinning")]
    public void TestUnpinWindowAndReopen()
    {
        string imagePath = Path.GetFullPath(@".\TestAssets\8.png");

        // Open image, pin it first, then unpin
        var initialWindow = OpenPeekWindow(imagePath);
        var originalBounds = GetWindowBounds(initialWindow);

        // Move window to a custom position
        NativeMethods.MoveWindow(initialWindow, originalBounds.X + 250, originalBounds.Y + 125);
        var movedBounds = GetWindowBounds(initialWindow);

        // Pin then unpin to ensure we test the unpinned state
        PinWindow();
        UnpinWindow();

        // Close peek
        ClosePeekAndExplorer();

        // Reopen the same image
        var reopenedWindow = OpenPeekWindow(imagePath);
        var reopenedBounds = GetWindowBounds(reopenedWindow);

        // Verify window opened at default position (not the previous moved position)
        bool openedAtDefault = Math.Abs(movedBounds.X - reopenedBounds.X) > 50 ||
                              Math.Abs(movedBounds.Y - reopenedBounds.Y) > 50;

        Assert.IsTrue(openedAtDefault, "Unpinned window should open at default position, not previous moved position");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test opening file with default program by clicking a button
    /// </summary>
    [TestMethod("Peek.OpenWithDefaultProgram.ClickButton")]
    [TestCategory("Open with default program")]
    public void TestOpenWithDefaultProgramByButton()
    {
        string zipPath = Path.GetFullPath(@".\TestAssets\7.zip");

        // Open zip file with Peek
        var peekWindow = OpenPeekWindow(zipPath);

        // Find and click the "Open with default program" button
        var openButton = FindLaunchButton();
        Assert.IsNotNull(openButton, "Open with default program button should be found");

        // Click the button to open with default program
        openButton.Click();

        // Wait a moment for the default program to launch
        Thread.Sleep(2000);

        // Verify that the default program process has started (check for Explorer opening 7-zip)
        bool defaultProgramLaunched = CheckIfExplorerLaunched();
        Assert.IsTrue(defaultProgramLaunched, "Default program (Explorer/7-zip) should be launched after clicking the button");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test opening file with default program by pressing Enter key
    /// </summary>
    [TestMethod("Peek.OpenWithDefaultProgram.PressEnter")]
    [TestCategory("Open with default program")]
    public void TestOpenWithDefaultProgramByEnter()
    {
        string zipPath = Path.GetFullPath(@".\TestAssets\7.zip");

        // Open zip file with Peek
        var peekWindow = OpenPeekWindow(zipPath);

        // Press Enter key to open with default program
        SendKeys(Key.Enter);

        // Wait a moment for the default program to launch
        Thread.Sleep(2000);

        // Verify that the default program process has started (check for Explorer opening 7-zip)
        bool defaultProgramLaunched = CheckIfExplorerLaunched();
        Assert.IsTrue(defaultProgramLaunched, "Default program (Explorer/7-zip) should be launched after pressing Enter");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test switching between files in a folder using Left and Right arrow keys
    /// </summary>
    [TestMethod("Peek.FileNavigation.SwitchFilesWithArrowKeys")]
    [TestCategory("File Navigation")]
    public void TestSwitchFilesWithArrowKeys()
    {
        // Get all files in TestAssets folder, ordered alphabetically
        var testFiles = GetTestAssetFiles();

        // Start with the first file in the TestAssets folder
        string firstFilePath = testFiles[0];
        var peekWindow = OpenPeekWindow(firstFilePath);

        // Keep track of visited files to ensure we can navigate through all
        var visitedFiles = new List<string> { Path.GetFileNameWithoutExtension(firstFilePath) };

        // Navigate forward through files using Right arrow
        for (int i = 1; i < testFiles.Count; i++)
        {
            // Press Right arrow to go to next file
            SendKeys(Key.Right);

            // Wait for file to load
            Thread.Sleep(2000);

            // Try to determine current file from window title
            var currentWindow = peekWindow.Name;
            string expectedFileName = Path.GetFileNameWithoutExtension(testFiles[i]);
            if (!string.IsNullOrEmpty(currentWindow) && currentWindow.StartsWith(expectedFileName, StringComparison.Ordinal))
            {
                visitedFiles.Add(expectedFileName);
            }
        }

        // Verify we navigated through the expected number of files
        Assert.AreEqual(testFiles.Count, visitedFiles.Count, $"Should have navigated through all {testFiles.Count} files, but only visited {visitedFiles.Count} files: {string.Join(", ", visitedFiles)}");

        // Navigate backward using Left arrow to verify reverse navigation
        for (int i = testFiles.Count - 2; i >= 0; i--)
        {
            SendKeys(Key.Left);

            // Wait for file to load
            Thread.Sleep(2000);

            // Try to determine current file from window title during backward navigation
            var currentWindow = peekWindow.Name;
            string expectedFileName = Path.GetFileNameWithoutExtension(testFiles[i]);
            if (!string.IsNullOrEmpty(currentWindow) && currentWindow.StartsWith(expectedFileName, StringComparison.Ordinal))
            {
                // Remove the last visited file (going backward)
                if (visitedFiles.Count > 1)
                {
                    visitedFiles.RemoveAt(visitedFiles.Count - 1);
                }
            }
        }

        // Verify backward navigation worked - should be back to the first file
        Assert.AreEqual(1, visitedFiles.Count, $"After backward navigation, should be back to first file only. Remaining files: {string.Join(", ", visitedFiles)}");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Test switching between multiple selected files
    /// Select first 3 files in Explorer, open with Peek, verify you can switch only between selected files using arrow keys
    /// </summary>
    [TestMethod("Peek.FileNavigation.SwitchBetweenSelectedFiles")]
    [TestCategory("File Navigation")]
    public void TestSwitchBetweenSelectedFiles()
    {
        // Get first 3 files in TestAssets folder, ordered alphabetically
        var allFiles = GetTestAssetFiles();
        var selectedFiles = allFiles.Take(3).ToList();

        // Open Explorer and select the first file
        Session.StartExe("explorer.exe", $"/select,\"{selectedFiles[0]}\"");

        // Wait for Explorer to open and select the first file
        WaitForExplorerWindow(selectedFiles[0]);

        // Give Explorer time to fully load
        Thread.Sleep(2000);

        // Use Shift+Down to extend selection to include the next 2 files
        SendKeys(Key.Shift, Key.Down); // Extend to second file
        Thread.Sleep(300);
        SendKeys(Key.Shift, Key.Down); // Extend to third file
        Thread.Sleep(300);

        // Now we should have the first 3 files selected, open Peek
        SendPeekHotkeyWithRetry();

        // Find the peek window (should open with last selected file when multiple files are selected)
        var peekWindow = FindPeekWindow(selectedFiles[2]); // Third file (last selected)
        string lastFileName = Path.GetFileNameWithoutExtension(selectedFiles[2]);

        // Keep track of visited files during navigation (starting from the last file)
        var visitedFiles = new List<string> { lastFileName };
        var expectedFileNames = selectedFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

        // Test navigation by pressing Left arrow multiple times to verify we only cycle through 3 selected files
        var windowTitles = new List<string> { peekWindow.Name };

        // Press Left arrow 5 times (more than the 3 selected files) to see if we cycle through only the selected files
        for (int i = 0; i < 5; i++)
        {
            SendKeys(Key.Left);
            Thread.Sleep(2000); // Wait for file to load

            var currentWindowTitle = peekWindow.Name;
            windowTitles.Add(currentWindowTitle);
        }

        // Analyze the navigation pattern - we should see repetition indicating we're only cycling through 3 files
        var uniqueWindowsVisited = windowTitles.Distinct().Count();

        // We should see at most 3 unique windows (the 3 selected files), even after 6 navigation steps
        Assert.IsTrue(uniqueWindowsVisited <= 3, $"Should only navigate through the 3 selected files, but found {uniqueWindowsVisited} unique windows. " + $"Window titles: {string.Join(" -> ", windowTitles)}");

        ClosePeekAndExplorer();
    }

    private bool CheckIfExplorerLaunched()
    {
        var possibleTitles = new[]
        {
            "7.zip - File Explorer",
            "7 - File Explorer",
            "7",
            "7.zip",
        };

        foreach (var title in possibleTitles)
        {
            try
            {
                var explorerWindow = Find(title, 5000, true);
                if (explorerWindow != null)
                {
                    return true;
                }
            }
            catch
            {
                // Continue to next title
            }
        }

        return false;
    }

    private void OpenAndPeekFile(string fullPath)
    {
        Session.StartExe("explorer.exe", $"/select,\"{fullPath}\"");

        // Wait for Explorer to open and become ready
        WaitForExplorerWindow(fullPath);

        // Send Peek hotkey with retry mechanism
        SendPeekHotkeyWithRetry();
    }

    private void WaitForExplorerWindow(string filePath)
    {
        WaitForCondition(
            condition: () =>
            {
                try
                {
                    // Check if Explorer window is open and responsive
                    var explorerProcesses = Process.GetProcessesByName("explorer")
                        .Where(p => p.MainWindowHandle != IntPtr.Zero)
                        .ToList();

                    if (explorerProcesses.Count != 0)
                    {
                        // Give Explorer a moment to fully load the file selection
                        Thread.Sleep(ExplorerLoadDelayMs);

                        // Verify the file is accessible
                        return File.Exists(filePath) || Directory.Exists(filePath);
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WaitForExplorerWindow exception: {ex.Message}");
                    return false;
                }
            },
            timeoutSeconds: ExplorerOpenTimeoutSeconds,
            checkIntervalMs: ExplorerCheckIntervalMs,
            timeoutMessage: $"Explorer window did not open for file: {filePath}");
    }

    private void SendPeekHotkeyWithRetry()
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                // Send the Peek hotkey
                SendKeys(Key.LCtrl, Key.Space);

                // Wait for Peek window to appear
                if (WaitForPeekWindow())
                {
                    return; // Success
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendPeekHotkeyWithRetry attempt {attempt} failed: {ex.Message}");

                if (attempt == MaxRetryAttempts)
                {
                    throw new InvalidOperationException($"Failed to open Peek after {MaxRetryAttempts} attempts. Last error: {ex.Message}", ex);
                }
            }

            // Wait before retry using Thread.Sleep
            Thread.Sleep(RetryDelayMs);
        }

        throw new InvalidOperationException($"Failed to open Peek after {MaxRetryAttempts} attempts");
    }

    private bool WaitForPeekWindow()
    {
        try
        {
            WaitForCondition(
                condition: () =>
                {
                    if (TryFindPeekWindow())
                    {
                        // Give Peek a moment to fully initialize using Thread.Sleep
                        Thread.Sleep(PeekInitializeDelayMs);
                        return true;
                    }

                    return false;
                },
                timeoutSeconds: PeekWindowTimeoutSeconds,
                checkIntervalMs: PeekCheckIntervalMs,
                timeoutMessage: "Peek window did not appear");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WaitForPeekWindow failed: {ex.Message}");
            return false;
        }
    }

    private bool WaitForCondition(Func<bool> condition, int timeoutSeconds, int checkIntervalMs, string timeoutMessage)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log exception but continue waiting
                Debug.WriteLine($"WaitForCondition exception: {ex.Message}");
            }

            // Use async delay to prevent blocking the thread
            Thread.Sleep(checkIntervalMs);
        }

        throw new TimeoutException($"{timeoutMessage} (timeout: {timeoutSeconds}s)");
    }

    private bool TryFindPeekWindow()
    {
        try
        {
            // Check for Peek process with timeout
            var peekProcesses = Process.GetProcessesByName("PowerToys.Peek.UI")
                .Where(p => p.MainWindowHandle != IntPtr.Zero);

            var foundProcess = peekProcesses.Any();

            if (foundProcess)
            {
                // Additional validation - check if window is responsive
                Thread.Sleep(100); // Small delay to ensure window is ready
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryFindPeekWindow exception: {ex.Message}");
            return false;
        }
    }

    private Element OpenPeekWindow(string filePath)
    {
        try
        {
            SendKeys(Key.Enter);

            // Open file with Peek
            OpenAndPeekFile(filePath);

            // Find the Peek window using the common method with timeout
            var peekWindow = FindPeekWindow(filePath);

            // Attach to the found window with error handling
            try
            {
                Session.Attach(peekWindow.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to attach to window: {ex.Message}");
            }

            return peekWindow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenPeekWindow failed for {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Test a single file preview with visual comparison
    /// </summary>
    /// <param name="filePath">Full path to the file to test</param>
    /// <param name="expectedFileName">Expected file name for visual comparison</param>
    private void TestSingleFilePreview(string filePath, string expectedFileName, int? delayMs = 5000)
    {
        Element? previewWindow = null;

        try
        {
            Debug.WriteLine($"Testing file preview: {Path.GetFileName(filePath)}");

            previewWindow = OpenPeekWindow(filePath);

            if (delayMs.HasValue)
            {
                Thread.Sleep(delayMs.Value); // Allow time for the preview to load
            }

            Assert.IsNotNull(previewWindow, $"Should open Peek window for {Path.GetFileName(filePath)}");

            // Perform visual comparison
            VisualAssert.AreEqual(TestContext, previewWindow, expectedFileName);

            Debug.WriteLine($"Successfully tested: {Path.GetFileName(filePath)}");
        }
        finally
        {
            // Always cleanup in finally block
            ClosePeekAndExplorer();
        }
    }

    private Rectangle GetWindowBounds(Element window)
    {
        if (window.Rect == null)
        {
            return Rectangle.Empty;
        }
        else
        {
            return window.Rect.Value;
        }
    }

    private void PinWindow()
    {
        // Find pin button using AutomationId
        var pinButton = Find(By.AccessibilityId("PinButton"), 2000);
        Assert.IsNotNull(pinButton, "Pin button should be found");

        pinButton.Click();
        Thread.Sleep(PinActionDelayMs); // Wait for pin action to complete
    }

    private void UnpinWindow()
    {
        // Find pin button using AutomationId (same button, just toggle the state)
        var pinButton = Find(By.AccessibilityId("PinButton"), 2000);
        Assert.IsNotNull(pinButton, "Pin button should be found");

        pinButton.Click();
        Thread.Sleep(PinActionDelayMs); // Wait for unpin action to complete
    }

    private void ClosePeekAndExplorer()
    {
        try
        {
            // Close Peek window
            Session.CloseMainWindow();
            Thread.Sleep(500);
            SendKeys(Key.Win, Key.M);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error closing Peek window: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all files in TestAssets folder, ordered alphabetically, excluding hidden files
    /// </summary>
    /// <returns>List of file paths in alphabetical order</returns>
    private List<string> GetTestAssetFiles()
    {
        string testAssetsPath = Path.GetFullPath(@".\TestAssets");
        return Directory.GetFiles(testAssetsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .OrderBy(file => file)
            .ToList();
    }

    /// <summary>
    /// Find Peek window by trying both filename with and without extension
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    /// <param name="timeout">Timeout in milliseconds</param>
    /// <returns>The found Peek window element</returns>
    private Element FindPeekWindow(string filePath, int timeout = 5000)
    {
        string fileName = Path.GetFileName(filePath);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        // Try both window title formats since Windows may show or hide file extensions
        string peekWindowTitleWithExt = $"{fileName} - Peek";
        string peekWindowTitleWithoutExt = $"{fileNameWithoutExt} - Peek";

        Element? peekWindow = null;

        try
        {
            // First try to find the window with extension
            peekWindow = Find(peekWindowTitleWithoutExt, timeout, true);
        }
        catch
        {
            try
            {
                // Then try without extension
                peekWindow = Find(peekWindowTitleWithExt, timeout, true);
            }
            catch
            {
                // If neither works, let it fail with a clear message
                Assert.Fail($"Could not find Peek window with title '{peekWindowTitleWithExt}' or '{peekWindowTitleWithoutExt}'");
            }
        }

        Assert.IsNotNull(peekWindow, $"Should find Peek window for file: {Path.GetFileName(filePath)}");

        return peekWindow;
    }

    /// <summary>
    /// Helper method to find the launch button with different AccessibilityIds depending on window size
    /// </summary>
    /// <returns>The launch button element</returns>
    private Element? FindLaunchButton()
    {
        try
        {
            // Try to find button with ID for larger window first
            var button = Find(By.AccessibilityId("LaunchAppButton_Text"), 1000);
            if (button != null)
            {
                return button;
            }
        }
        catch
        {
            // Try to find button with ID for smaller window
            var button = Find(By.AccessibilityId("LaunchAppButton"), 1000);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }
}
