// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    private const string PeekProcessName = "PowerToys.Peek.UI";
    private const int ExplorerOpenTimeoutMS = 15_000;
    private const int PeekWindowTimeoutMS = 15_000;
    private const int MaxHotkeyAttempts = 3;

    private long explorerWindowHandle;

    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical, enableModules: new[] { "Peek" })
    {
    }

    static PeekFilePreviewTests()
    {
        SettingsConfigHelper.UpdateModuleSettings(
            "Peek",
            """
            {
              "name": "Peek",
              "version": "1.0",
              "properties": {}
            }
            """,
            settings =>
            {
                var properties = settings["properties"] as JsonObject ?? new JsonObject();
                properties["ActivationShortcut"] = new JsonObject
                {
                    ["win"] = false,
                    ["ctrl"] = true,
                    ["alt"] = false,
                    ["shift"] = false,
                    ["code"] = 32,
                    ["key"] = "Space",
                };
                properties["AlwaysRunNotElevated"] = new JsonObject { ["value"] = true };
                properties["CloseAfterLosingFocus"] = new JsonObject { ["value"] = false };
                properties["ConfirmFileDelete"] = new JsonObject { ["value"] = true };
                properties["EnableSpaceToActivate"] = new JsonObject { ["value"] = false };
                settings["properties"] = properties;
            });
    }

    protected override IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        "PowerToys.FancyZonesEditor",
        PeekProcessName,
    };

    [TestInitialize]
    public void PreparePeekTest()
    {
        WindowControl.TryCloseByApp("PowerToys.Settings");
    }

    [TestCleanup]
    public void CleanupPeekTest()
    {
        ClosePeekAndExplorer();
    }

    [TestMethod("Peek.FilePreview.Folder")]
    [TestCategory("Preview files")]
    public void PeekFolderFilePreview()
    {
        var folderPath = Path.GetFullPath(@".\TestAssets");
        var peekWindow = OpenPeekWindow(folderPath);

        peekWindow.Find<TextBlock>(By.Name("File Type: File folder"), 5_000);
    }

    [TestMethod("Peek.FilePreview.JPEGImage")]
    [TestCategory("Preview files")]
    public void PeekJPEGImagePreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\2.jpg"), "2");
    }

    [TestMethod("Peek.FilePreview.QOIImage")]
    [TestCategory("Preview files")]
    public void PeekQOIImagePreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\4.qoi"), "4");
    }

    [TestMethod("Peek.FilePreview.CPPSourceCode")]
    [TestCategory("Preview files")]
    public void PeekCPPSourceCodePreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\5.cpp"), "5");
    }

    [TestMethod("Peek.FilePreview.MarkdownDocument")]
    [TestCategory("Preview files")]
    public void PeekMarkdownDocumentPreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\6.md"), "6");
    }

    [TestMethod("Peek.FilePreview.ZIPArchive")]
    [TestCategory("Preview files")]
    public void PeekZIPArchivePreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\7.zip"), "7");
    }

    [TestMethod("Peek.FilePreview.PNGImage")]
    [TestCategory("Preview files")]
    public void PeekPNGImagePreview()
    {
        TestSingleFilePreview(Path.GetFullPath(@".\TestAssets\8.png"), "8");
    }

    [TestMethod("Peek.WindowPinning.PinAndSwitchImages")]
    [TestCategory("Window Pinning")]
    public void TestPinWindowAndSwitchImages()
    {
        var firstImagePath = Path.GetFullPath(@".\TestAssets\8.png");
        var secondImagePath = Path.GetFullPath(@".\TestAssets\2.jpg");
        var initialWindow = OpenPeekWindow(firstImagePath);
        var movedBounds = MoveWindowBy(initialWindow, 100, 50);

        ClickPinButton(initialWindow);
        ClosePeekAndExplorer();

        var secondWindow = OpenPeekWindow(secondImagePath);
        AssertBoundsEqual(movedBounds, GetWindowBounds(secondWindow), "when switching images while pinned");
    }

    [TestMethod("Peek.WindowPinning.PinAndReopen")]
    [TestCategory("Window Pinning")]
    public void TestPinWindowAndReopen()
    {
        var imagePath = Path.GetFullPath(@".\TestAssets\8.png");
        var initialWindow = OpenPeekWindow(imagePath);
        var movedBounds = MoveWindowBy(initialWindow, 150, 75);

        ClickPinButton(initialWindow);
        ClosePeekAndExplorer();

        var reopenedWindow = OpenPeekWindow(imagePath);
        AssertBoundsEqual(movedBounds, GetWindowBounds(reopenedWindow), "after reopening while pinned");
    }

    [TestMethod("Peek.WindowPinning.UnpinAndSwitchFiles")]
    [TestCategory("Window Pinning")]
    public void TestUnpinWindowAndSwitchFiles()
    {
        var firstFilePath = Path.GetFullPath(@".\TestAssets\8.png");
        var secondFilePath = Path.GetFullPath(@".\TestAssets\2.jpg");
        var pinnedWindow = OpenPeekWindow(firstFilePath);
        var movedBounds = MoveWindowBy(pinnedWindow, 200, 100);
        var movedCenter = movedBounds.Center;

        ClickPinButton(pinnedWindow);
        ClickPinButton(pinnedWindow);
        ClosePeekAndExplorer();

        var unpinnedWindow = OpenPeekWindow(secondFilePath);
        var unpinnedBounds = GetWindowBounds(unpinnedWindow);
        var unpinnedCenter = unpinnedBounds.Center;

        var sizeChanged = Math.Abs(movedBounds.Width - unpinnedBounds.Width) > 10 ||
                          Math.Abs(movedBounds.Height - unpinnedBounds.Height) > 10;
        var centerChanged = Math.Abs(movedCenter.X - unpinnedCenter.X) > 50 ||
                            Math.Abs(movedCenter.Y - unpinnedCenter.Y) > 50;

        Assert.IsTrue(sizeChanged, "Window size should be different for different file types.");
        Assert.IsTrue(centerChanged, "Window center should move to its default position when unpinned.");
    }

    [TestMethod("Peek.WindowPinning.UnpinAndReopen")]
    [TestCategory("Window Pinning")]
    public void TestUnpinWindowAndReopen()
    {
        var imagePath = Path.GetFullPath(@".\TestAssets\8.png");
        var initialWindow = OpenPeekWindow(imagePath);
        var movedBounds = MoveWindowBy(initialWindow, 250, 125);

        ClickPinButton(initialWindow);
        ClickPinButton(initialWindow);
        ClosePeekAndExplorer();

        var reopenedWindow = OpenPeekWindow(imagePath);
        var reopenedBounds = GetWindowBounds(reopenedWindow);
        var openedAtDefault = Math.Abs(movedBounds.Left - reopenedBounds.Left) > 50 ||
                              Math.Abs(movedBounds.Top - reopenedBounds.Top) > 50;

        Assert.IsTrue(openedAtDefault, "Unpinned window should open at its default position.");
    }

    [TestMethod("Peek.OpenWithDefaultProgram.ClickButton")]
    [TestCategory("Open with default program")]
    public void TestOpenWithDefaultProgramByButton()
    {
        var zipPath = Path.GetFullPath(@".\TestAssets\7.zip");
        var peekWindow = OpenPeekWindow(zipPath);

        peekWindow.Find<Button>(By.AccessibilityId("LaunchAppButton"), 5_000).Click();

        Assert.IsTrue(
            WaitForExplorerTitle(Path.GetFileNameWithoutExtension(zipPath), 10_000),
            "The default program did not open the ZIP archive after clicking the launch button.");
    }

    [TestMethod("Peek.OpenWithDefaultProgram.PressEnter")]
    [TestCategory("Open with default program")]
    public void TestOpenWithDefaultProgramByEnter()
    {
        var zipPath = Path.GetFullPath(@".\TestAssets\7.zip");
        var peekWindow = OpenPeekWindow(zipPath);

        peekWindow.Find<Button>(By.AccessibilityId("LaunchAppButton"), 5_000).Focus();
        KeyboardHelper.SendKeys(Key.Enter);

        Assert.IsTrue(
            WaitForExplorerTitle(Path.GetFileNameWithoutExtension(zipPath), 10_000),
            "The default program did not open the ZIP archive after pressing Enter.");
    }

    [TestMethod("Peek.FileNavigation.SwitchFilesWithArrowKeys")]
    [TestCategory("File Navigation")]
    public void TestSwitchFilesWithArrowKeys()
    {
        var testFiles = GetTestAssetFiles();
        var peekWindow = OpenPeekWindow(testFiles[0]);

        for (var index = 1; index < testFiles.Count; index++)
        {
            peekWindow.EnsureForeground();
            KeyboardHelper.SendKeys(Key.Right);
            peekWindow = WaitForPeekWindow(testFiles[index], PeekWindowTimeoutMS)
                ?? throw new AssertFailedException($"Peek did not navigate to {Path.GetFileName(testFiles[index])}.");
            EnsurePeekReady(peekWindow);
        }

        for (var index = testFiles.Count - 2; index >= 0; index--)
        {
            peekWindow.EnsureForeground();
            KeyboardHelper.SendKeys(Key.Left);
            peekWindow = WaitForPeekWindow(testFiles[index], PeekWindowTimeoutMS)
                ?? throw new AssertFailedException($"Peek did not navigate back to {Path.GetFileName(testFiles[index])}.");
            EnsurePeekReady(peekWindow);
        }
    }

    [TestMethod("Peek.FileNavigation.SwitchBetweenSelectedFiles")]
    [TestCategory("File Navigation")]
    public void TestSwitchBetweenSelectedFiles()
    {
        var selectedFiles = GetTestAssetFiles().Take(3).ToList();
        var explorerWindow = OpenExplorerAndSelect(selectedFiles[0]);

        explorerWindow.EnsureForeground();
        KeyboardHelper.SendKeys(Key.Shift, Key.Down);
        KeyboardHelper.SendKeys(Key.Shift, Key.Down);

        var peekWindow = SendPeekHotkeyWithRetry(selectedFiles[2]);
        EnsurePeekReady(peekWindow);
        var expectedNames = selectedFiles.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FileNameFromTitle(peekWindow.WindowTitle, expectedNames),
        };

        for (var index = 0; index < 5; index++)
        {
            var previousTitle = peekWindow.WindowTitle;
            peekWindow.EnsureForeground();
            KeyboardHelper.SendKeys(Key.Left);
            peekWindow = WaitForSelectedFileChange(previousTitle, expectedNames, PeekWindowTimeoutMS)
                ?? throw new AssertFailedException("Peek did not switch to another selected file.");
            EnsurePeekReady(peekWindow);
            visitedNames.Add(FileNameFromTitle(peekWindow.WindowTitle, expectedNames));
        }

        Assert.IsTrue(visitedNames.Count > 1, "Peek should switch between the selected files.");
        Assert.IsTrue(
            visitedNames.Count <= selectedFiles.Count,
            $"Peek should navigate only among the selected files, but visited: {string.Join(", ", visitedNames)}.");
    }

    private Session OpenPeekWindow(string filePath)
    {
        OpenExplorerAndSelect(filePath);
        var peekWindow = SendPeekHotkeyWithRetry(filePath);
        EnsurePeekReady(peekWindow);
        return peekWindow;
    }

    private Session OpenExplorerAndSelect(string filePath)
    {
        Assert.IsTrue(File.Exists(filePath) || Directory.Exists(filePath), $"Test asset does not exist: {filePath}");

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true,
        });

        var parentName = Path.GetFileName(Path.GetDirectoryName(filePath.TrimEnd(Path.DirectorySeparatorChar)));
        var explorerWindow = WindowsFinder.WaitForWindowByApp(
            "explorer",
            window => window.Title.Contains(parentName, StringComparison.OrdinalIgnoreCase),
            ExplorerOpenTimeoutMS);

        Assert.IsNotNull(explorerWindow, $"Explorer did not open the parent folder for {filePath}.");
        explorerWindowHandle = explorerWindow!.WindowHandle;
        explorerWindow.EnsureForeground();

        var selectedItemName = Path.GetFileName(filePath.TrimEnd(Path.DirectorySeparatorChar));
        Assert.IsTrue(
            explorerWindow.WaitFor(
                () => explorerWindow.FindAll<Element>(By.Name(selectedItemName), 0).Any(element => element.Selected),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"Explorer did not select {selectedItemName}.");

        return explorerWindow;
    }

    private Session SendPeekHotkeyWithRetry(string expectedPath)
    {
        for (var attempt = 1; attempt <= MaxHotkeyAttempts; attempt++)
        {
            KeyboardHelper.SendKeys(Key.Ctrl, Key.Space);
            var peekWindow = WaitForPeekWindow(expectedPath, PeekWindowTimeoutMS / MaxHotkeyAttempts);
            if (peekWindow is not null)
            {
                return peekWindow;
            }
        }

        var windows = string.Join(
            Environment.NewLine,
            WindowsFinder.ListByApp(PeekProcessName)
                .Select(window => $"hwnd={window.Hwnd}, title='{window.Title}', size={window.Width}x{window.Height}"));
        Assert.Fail(
            $"Peek did not open {Path.GetFileName(expectedPath)} after {MaxHotkeyAttempts} hotkey attempts." +
            Environment.NewLine +
            (windows.Length == 0 ? "No Peek windows were found." : windows));
        return null!;
    }

    private static Session? WaitForPeekWindow(string filePath, int timeoutMS)
    {
        return WindowsFinder.WaitForWindowByApp(
            PeekProcessName,
            window => WindowTitleMatches(window.Title, filePath),
            timeoutMS);
    }

    private static Session? WaitForSelectedFileChange(string previousTitle, HashSet<string> expectedNames, int timeoutMS)
    {
        return WindowsFinder.WaitForWindowByApp(
            PeekProcessName,
            window => !string.Equals(window.Title, previousTitle, StringComparison.OrdinalIgnoreCase) &&
                      expectedNames.Any(name => TitleMatchesName(window.Title, name)),
            timeoutMS);
    }

    private static bool WindowTitleMatches(string title, string filePath)
    {
        return TitleMatchesName(title, Path.GetFileName(filePath)) ||
               TitleMatchesName(title, Path.GetFileNameWithoutExtension(filePath));
    }

    private static bool TitleMatchesName(string title, string name)
    {
        return string.Equals(title, name, StringComparison.OrdinalIgnoreCase) ||
               title.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase) ||
               title.StartsWith(name + " -", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePeekReady(Session peekWindow)
    {
        peekWindow.Find<Button>(By.AccessibilityId("PinButton"), 15_000);
    }

    private static string FileNameFromTitle(string title, HashSet<string> expectedNames)
    {
        var match = expectedNames.FirstOrDefault(name => TitleMatchesName(title, name));
        Assert.IsFalse(string.IsNullOrEmpty(match), $"Unexpected Peek window title '{title}'.");
        return match!;
    }

    private static bool WaitForExplorerTitle(string title, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            if (WindowsFinder.ListByApp("explorer").Any(window => TitleMatchesName(window.Title, title)))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private void TestSingleFilePreview(string filePath, string expectedFileName)
    {
        var previewWindow = OpenPeekWindow(filePath);
        previewWindow.Find<Button>(By.AccessibilityId("PinButton"), 10_000);
        Thread.Sleep(3_000);
        VisualAssert.AreEqual(TestContext, previewWindow, expectedFileName);
    }

    private static WindowBounds MoveWindowBy(Session window, int offsetX, int offsetY)
    {
        var originalBounds = GetWindowBounds(window);
        WindowHelper.MoveWindow(
            new IntPtr(window.WindowHandle),
            originalBounds.Left + offsetX,
            originalBounds.Top + offsetY);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        WindowBounds movedBounds;
        do
        {
            movedBounds = GetWindowBounds(window);
            if (Math.Abs(movedBounds.Left - (originalBounds.Left + offsetX)) <= 5 &&
                Math.Abs(movedBounds.Top - (originalBounds.Top + offsetY)) <= 5)
            {
                return movedBounds;
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail("Peek window did not move to the requested position.");
        return default;
    }

    private static WindowBounds GetWindowBounds(Session window)
    {
        var (left, top, right, bottom) = WindowHelper.GetWindowBounds(new IntPtr(window.WindowHandle));
        Assert.IsTrue(right > left && bottom > top, "Peek window has invalid bounds.");
        return new WindowBounds(left, top, right - left, bottom - top);
    }

    private static void AssertBoundsEqual(WindowBounds expected, WindowBounds actual, string scenario)
    {
        Assert.AreEqual(expected.Left, actual.Left, 5, $"Window X position should remain the same {scenario}.");
        Assert.AreEqual(expected.Top, actual.Top, 5, $"Window Y position should remain the same {scenario}.");
        Assert.AreEqual(expected.Width, actual.Width, 10, $"Window width should remain the same {scenario}.");
        Assert.AreEqual(expected.Height, actual.Height, 10, $"Window height should remain the same {scenario}.");
    }

    private static void ClickPinButton(Session peekWindow)
    {
        peekWindow.Find<Button>(By.AccessibilityId("PinButton"), 5_000).Click(msPostAction: 500);
    }

    private static List<string> GetTestAssetFiles()
    {
        var testAssetsPath = Path.GetFullPath(@".\TestAssets");
        return Directory.GetFiles(testAssetsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ClosePeekAndExplorer()
    {
        WindowControl.TryCloseByApp(PeekProcessName);

        if (explorerWindowHandle == 0)
        {
            return;
        }

        WindowControl.TryBringToForeground(new IntPtr(explorerWindowHandle));
        KeyboardHelper.SendKeys(Key.Alt, Key.F4);
        explorerWindowHandle = 0;
    }

    private readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
    {
        public (int X, int Y) Center => (Left + (Width / 2), Top + (Height / 2));
    }
}