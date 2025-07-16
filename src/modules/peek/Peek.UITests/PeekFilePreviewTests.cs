// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    // Timeout constants for better maintainability
    private const int ExplorerOpenTimeoutSeconds = 15;
    private const int PeekWindowTimeoutSeconds = 10;
    private const int ExplorerLoadDelayMs = 3000;
    private const int ExplorerCheckIntervalMs = 1000;
    private const int PeekCheckIntervalMs = 1000;
    private const int PeekInitializeDelayMs = 3000;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 3000;
    private const int WindowMoveDelayMs = 500;
    private const int PinActionDelayMs = 500;

    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical)
    {
    }

    [TestMethod("Peek.FilePreview.Folder")]
    [TestCategory("Preview files")]
    public void PeekFolderFilePreview()
    {
        string folderFullPath = Path.GetFullPath(@".\TestAssets");

        OpenAndPeekFile(folderFullPath);

        Task.Delay(1000).Wait(); // Wait for Peek to load

        var peekWindow = Find("TestAssets - Peek", 1000, true);

        Assert.IsNotNull(peekWindow);

        Assert.IsNotNull(peekWindow.Find<TextBlock>("File Type: File folder", 500), "Folder preview should be loaded successfully");

        ClosePeekAndExplorer();
    }

    /// <summary>
    /// Comprehensive test for all files in TestAssets with visual comparison
    /// Tests all supported file types and validates preview rendering with image comparison
    /// </summary>
    [TestMethod("Peek.FilePreview.AllTestAssets")]
    [TestCategory("Preview files")]
    public void PeekAllTestAssets()
    {
        // Get all test asset files
        string testAssetsPath = Path.GetFullPath(@".\TestAssets");
        var testFiles = Directory.GetFiles(testAssetsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .OrderBy(file => file)
            .ToList();

        Assert.IsTrue(testFiles.Count > 0, "Should have test files in TestAssets folder");

        // Test each file individually with visual comparison
        foreach (var testFile in testFiles)
        {
            string fileName = Path.GetFileName(testFile);
            string fileExtension = Path.GetExtension(testFile).ToLowerInvariant();

            TestFilePreviewWithVisualComparison(testFile);
        }

        // Assert.Fail("All test files should be processed without failure. If this fails, check the TestAssets folder for missing or unsupported files.");
    }

    /// <summary>
    /// Test window pinning functionality - pin window and switch between different sized images
    /// Verify the window stays at the same place and the same size
    /// </summary>
    [TestMethod("Peek.WindowPinning.PinAndSwitchImages")]
    [TestCategory("Window Pinning")]
    public void TestPinWindowAndSwitchImages()
    {
        // Use two different image files (assuming 8.png and another image with different size)
        string firstImagePath = Path.GetFullPath(@".\TestAssets\8.png");
        string secondImagePath = Path.GetFullPath(@".\TestAssets\2.jpg"); // Different format/size

        // Open first image
        var initialWindow = OpenPeekWindow(firstImagePath);

        var originalBounds = GetWindowBounds(initialWindow);

        // Move window to a custom position to test pin functionality
        MoveWindow(initialWindow, originalBounds.X + 100, originalBounds.Y + 50);
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
        MoveWindow(initialWindow, originalBounds.X + 150, originalBounds.Y + 75);
        var movedBounds = GetWindowBounds(initialWindow);

        // Pin the window
        PinWindow();

        // Close peek
        ClosePeekAndExplorer();
        Task.Delay(1000).Wait(); // Wait for window to close completely

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
        MoveWindow(pinnedWindow, originalBounds.X + 200, originalBounds.Y + 100);
        var movedBounds = GetWindowBounds(pinnedWindow);

        // Calculate the center point of the moved window
        int movedCenterX = movedBounds.X + (movedBounds.Width / 2);
        int movedCenterY = movedBounds.Y + (movedBounds.Height / 2);

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
        int unpinnedCenterX = unpinnedBounds.X + (unpinnedBounds.Width / 2);
        int unpinnedCenterY = unpinnedBounds.Y + (unpinnedBounds.Height / 2);

        // Verify window size is different (since it's a different file type)
        bool sizeChanged = Math.Abs(movedBounds.Width - unpinnedBounds.Width) > 10 ||
                          Math.Abs(movedBounds.Height - unpinnedBounds.Height) > 10;

        // Verify window center moved to default position (should be different from moved center)
        bool centerChanged = Math.Abs(movedCenterX - unpinnedCenterX) > 50 ||
                            Math.Abs(movedCenterY - unpinnedCenterY) > 50;

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
        var originalBounds = initialWindow.Rect ?? GetWindowBounds(initialWindow);

        // Move window to a custom position
        MoveWindow(initialWindow, originalBounds.X + 250, originalBounds.Y + 125);
        var movedBounds = initialWindow.Rect ?? GetWindowBounds(initialWindow);

        // Pin then unpin to ensure we test the unpinned state
        PinWindow();
        UnpinWindow();

        // Close peek
        ClosePeekAndExplorer();
        Task.Delay(1000).Wait();

        // Reopen the same image
        var reopenedWindow = OpenPeekWindow(imagePath);
        var reopenedBounds = GetWindowBounds(reopenedWindow);

        // Verify window opened at default position (not the previous moved position)
        bool openedAtDefault = Math.Abs(movedBounds.X - reopenedBounds.X) > 50 ||
                              Math.Abs(movedBounds.Y - reopenedBounds.Y) > 50;

        Assert.IsTrue(openedAtDefault, "Unpinned window should open at default position, not previous moved position");

        ClosePeekAndExplorer();
    }

    private void OpenAndPeekFile(string fullPath)
    {
        SendKeys(Key.Enter);

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
                // Check if Explorer window is open and responsive
                var explorerProcesses = Process.GetProcessesByName("explorer")
                    .Where(p => p.MainWindowHandle != IntPtr.Zero)
                    .ToList();

                if (explorerProcesses.Count != 0)
                {
                    // Give Explorer a moment to fully load the file selection
                    Task.Delay(ExplorerLoadDelayMs).Wait();

                    // Verify the file is accessible
                    return File.Exists(filePath) || Directory.Exists(filePath);
                }

                return false;
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
                if (attempt == MaxRetryAttempts)
                {
                    throw new InvalidOperationException($"Failed to open Peek after {MaxRetryAttempts} attempts. Last error: {ex.Message}", ex);
                }
            }

            // Wait before retry
            Task.Delay(RetryDelayMs).Wait();
        }
    }

    private bool WaitForPeekWindow()
    {
        WaitForCondition(
            condition: () =>
            {
                if (TryFindPeekWindow())
                {
                    // Give Peek a moment to fully initialize
                    Task.Delay(PeekInitializeDelayMs).Wait();
                    return true;
                }

                return false;
            },
            timeoutSeconds: PeekWindowTimeoutSeconds,
            checkIntervalMs: PeekCheckIntervalMs,
            timeoutMessage: "Peek window did not appear");
        return true;
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
            catch
            {
                // Continue waiting on errors
            }

            Task.Delay(checkIntervalMs).Wait();
        }

        throw new TimeoutException($"{timeoutMessage} (timeout: {timeoutSeconds}s)");
    }

    private bool TryFindPeekWindow()
    {
        try
        {
            // Check for Peek process
            var peekProcesses = Process.GetProcessesByName("PowerToys.Peek.UI")
                .Where(p => p.MainWindowHandle != IntPtr.Zero);

            return peekProcesses.Any();
        }
        catch
        {
            // Ignore all errors in detection
            return false;
        }
    }

    private Element OpenPeekWindow(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        // Try both window title formats since Windows may show or hide file extensions
        string peekWindowTitleWithExt = $"{fileName} - Peek";
        string peekWindowTitleWithoutExt = $"{fileNameWithoutExt} - Peek";

        // Open file with Peek
        OpenAndPeekFile(filePath);

        // Try to attach to the correct window title
        Element? previewWindow = null;

        try
        {
            // First try to find the window with extension
            previewWindow = Find(peekWindowTitleWithExt, 5000, true);
            Session.Attach(peekWindowTitleWithExt);
        }
        catch
        {
            try
            {
                // Then try without extension
                previewWindow = Find(peekWindowTitleWithoutExt, 5000, true);
                Session.Attach(peekWindowTitleWithoutExt);
            }
            catch
            {
                // If neither works, let it fail with a clear message
                Assert.Fail($"Could not find and attach to Peek window with title '{peekWindowTitleWithExt}' or '{peekWindowTitleWithoutExt}' for file {fileName}");
            }
        }

        Assert.IsNotNull(previewWindow, $"Should open Peek window for {fileName}");
        return previewWindow;
    }

    private void TestFilePreviewWithVisualComparison(string filePath)
    {
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        Element previewWindow = OpenPeekWindow(filePath);

        Assert.IsNotNull(previewWindow, $"Should open Peek window for {fileNameWithoutExt}");

        // previewWindow.SaveToPngFile(Path.Combine(ScreenshotDirectory ?? string.Empty, $"{fileNameWithoutExt}.png"));
        VisualAssert.AreEqual(TestContext, previewWindow, fileNameWithoutExt );

        // Close peek window
        ClosePeekAndExplorer();
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
        Task.Delay(PinActionDelayMs).Wait(); // Wait for pin action to complete
    }

    private void UnpinWindow()
    {
        // Find pin button using AutomationId (same button, just toggle the state)
        var pinButton = Find(By.AccessibilityId("PinButton"), 2000);
        Assert.IsNotNull(pinButton, "Pin button should be found");

        pinButton.Click();
        Task.Delay(PinActionDelayMs).Wait(); // Wait for unpin action to complete
    }

    private void ClosePeekAndExplorer()
    {
        // Close Peek window
        Session.CloseMainWindow();

        // Close Explorer window
        try
        {
            var explorerWindows = Process.GetProcessesByName("explorer");
            foreach (var explorer in explorerWindows)
            {
                // Only close explorer windows
                if (explorer.MainWindowHandle != IntPtr.Zero)
                {
                    explorer.CloseMainWindow();
                }
            }
        }
        catch
        {
            // Ignore errors when closing Explorer
        }
    }

    private void MoveWindow(Element window, int x, int y)
    {
        try
        {
            var windowHandle = IntPtr.Parse(window.GetAttribute("NativeWindowHandle") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            if (windowHandle != IntPtr.Zero)
            {
                SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, SWPNOSIZE | SWPNOZORDER);
                Task.Delay(WindowMoveDelayMs).Wait();
            }
        }
        catch
        {
        }
    }

    // Windows API for moving windows
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint SWPNOSIZE = 0x0001;
    private const uint SWPNOZORDER = 0x0004;
}
