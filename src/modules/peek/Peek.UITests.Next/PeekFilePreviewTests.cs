// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using SHDocVw;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    private const string PeekProcessName = "PowerToys.Peek.UI";
    private static readonly Guid ShellApplicationClassId = new("13709620-C279-11CE-A49E-444553540000");
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";
    private const int ExplorerOpenTimeoutMS = 30_000;
    private const int ExplorerOpenAttempts = 3;
    private const int ExplorerSelectionStableSamples = 4;
    private const int PeekWindowTimeoutMS = 30_000;
    private const int PreviewLoadTimeoutMS = 60_000;
    private const int PreviewOpenAttempts = 3;
    private const int MaxHotkeyAttempts = 3;
    private const int MaxNavigationAttempts = 3;
    private const int ShellViewSelect = 0x1;
    private const int ShellViewDeselectOthers = 0x4;
    private const int ShellViewEnsureVisible = 0x8;
    private const int ShellViewFocused = 0x10;

    private long explorerWindowHandle;
    private IReadOnlyList<string> expectedExplorerSelection = Array.Empty<string>();
    private string? expectedExplorerFocusedPath;

    private static bool appsUseLightThemeValueExisted;
    private static object? originalAppsUseLightThemeValue;
    private static RegistryValueKind originalAppsUseLightThemeValueKind;
    private static bool restoreAppsUseLightTheme;

    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical, enableModules: new[] { "Peek" })
    {
    }

    static PeekFilePreviewTests()
    {
        ForcePipelineLightTheme();

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

    [ClassCleanup]
    public static void RestoreAppTheme()
    {
        if (!restoreAppsUseLightTheme)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizeRegistryPath);
            if (appsUseLightThemeValueExisted)
            {
                key.SetValue(AppsUseLightThemeValueName, originalAppsUseLightThemeValue!, originalAppsUseLightThemeValueKind);
            }
            else
            {
                key.DeleteValue(AppsUseLightThemeValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
        }
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
        CloseTestWindows();
        WindowControl.TryCloseByApp("PowerToys.Settings");
    }

    [TestCleanup]
    public void CleanupPeekTest()
    {
        CloseTestWindows();
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
        CloseTestWindows();

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
        CloseTestWindows();

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
        CloseTestWindows(preservePeekProcess: false);

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
        CloseTestWindows(preservePeekProcess: false);

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

        peekWindow.Find<Button>(By.AccessibilityId("LaunchAppButton"), 5_000).Invoke();

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
            peekWindow = NavigateToFileWithRetry(peekWindow, Key.Right, testFiles[index]);
        }

        for (var index = testFiles.Count - 2; index >= 0; index--)
        {
            peekWindow = NavigateToFileWithRetry(peekWindow, Key.Left, testFiles[index]);
        }
    }

    [TestMethod("Peek.FileNavigation.SwitchBetweenSelectedFiles")]
    [TestCategory("File Navigation")]
    public void TestSwitchBetweenSelectedFiles()
    {
        var selectedFiles = GetTestAssetFiles().Take(3).ToList();
        var explorerWindow = OpenExplorerAndSelect(selectedFiles[0]);
        SetExplorerSelection(explorerWindow, selectedFiles, selectedFiles[^1]);

        var peekWindow = SendPeekHotkeyWithRetry(selectedFiles[2]);
        EnsurePeekWindowInteractive(peekWindow);
        var expectedNames = selectedFiles.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FileNameFromTitle(peekWindow.WindowTitle, expectedNames),
        };

        for (var index = 0; index < 5; index++)
        {
            var previousTitle = peekWindow.WindowTitle;
            EnsurePeekWindowInteractive(peekWindow);
            EnsurePeekWindowForeground(peekWindow);
            KeyboardHelper.SendKeys(Key.Left);
            peekWindow = WaitForSelectedFileChange(previousTitle, expectedNames, PeekWindowTimeoutMS)
                ?? throw new AssertFailedException("Peek did not switch to another selected file.");
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

        for (var attempt = 1; attempt <= PreviewOpenAttempts; attempt++)
        {
            try
            {
                var peekWindow = SendPeekHotkeyWithRetry(filePath);
                EnsurePeekReady(peekWindow);
                return peekWindow;
            }
            catch (AssertFailedException ex) when (attempt < PreviewOpenAttempts)
            {
                TestContext.WriteLine(
                    $"Peek activation/readiness attempt {attempt}/{PreviewOpenAttempts} failed for " +
                    $"'{Path.GetFileName(filePath)}': {ex.Message}. Restarting Peek before retrying activation.");
                WindowControl.TryCloseByApp(PeekProcessName, timeoutMS: 10_000);
                Assert.IsTrue(StopPeekProcess(), "Peek did not stop before the next activation attempt.");
            }
        }

        Assert.Fail($"Peek did not become ready for '{Path.GetFileName(filePath)}'.");
        return null!;
    }

    private Session OpenExplorerAndSelect(string filePath)
    {
        Assert.IsTrue(File.Exists(filePath) || Directory.Exists(filePath), $"Test asset does not exist: {filePath}");

        var normalizedPath = filePath.TrimEnd(Path.DirectorySeparatorChar);
        var selectedItemName = Path.GetFileName(normalizedPath);
        TestContext.WriteLine(GetActivationDiagnostics($"Opening Explorer for '{normalizedPath}'"));

        for (var attempt = 1; attempt <= ExplorerOpenAttempts; attempt++)
        {
            CloseExplorerFileWindows();

            var existingHandles = WindowsFinder.ListByApp("explorer")
                .Where(IsExplorerFileWindow)
                .Select(window => window.Hwnd)
                .ToHashSet();

            using var launchProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/n,/select,\"{normalizedPath}\"",
                UseShellExecute = true,
            });
            TestContext.WriteLine(
                $"Explorer launch attempt {attempt}/{ExplorerOpenAttempts}: " +
                $"launcherPid={launchProcess?.Id.ToString() ?? "unknown"}, existingHwnds=[{string.Join(", ", existingHandles)}].");

            var explorerWindow = WindowsFinder.WaitForWindowByApp(
                "explorer",
                window => IsExplorerFileWindow(window) && !existingHandles.Contains(window.Hwnd),
                ExplorerOpenTimeoutMS);

            if (explorerWindow is null)
            {
                TestContext.WriteLine(GetActivationDiagnostics($"No fresh Explorer HWND after launch attempt {attempt}"));
                continue;
            }

            var expectedExplorerTitle = Path.GetFileName(Path.GetDirectoryName(normalizedPath));
            if (!WaitForExplorerWindowTitle(explorerWindow.WindowHandle, expectedExplorerTitle, ExplorerOpenTimeoutMS))
            {
                TestContext.WriteLine(
                    GetActivationDiagnostics(
                        $"Explorer HWND {explorerWindow.WindowHandle} did not navigate to '{expectedExplorerTitle}' after launch attempt {attempt}"));
                continue;
            }

            if (!SetAndWaitForExplorerSelection(
                    explorerWindow.WindowHandle,
                    [normalizedPath],
                    normalizedPath,
                    ExplorerOpenTimeoutMS))
            {
                TestContext.WriteLine(
                    GetActivationDiagnostics(
                        $"Explorer HWND {explorerWindow.WindowHandle} did not become foreground with a stable '{selectedItemName}' selection after launch attempt {attempt}"));
                continue;
            }

            explorerWindowHandle = explorerWindow.WindowHandle;
            expectedExplorerSelection = [normalizedPath];
            expectedExplorerFocusedPath = normalizedPath;
            TestContext.WriteLine(
                $"Explorer ready for '{selectedItemName}': hwnd={explorerWindow.WindowHandle}, " +
                $"pid={explorerWindow.ProcessId}, title='{explorerWindow.WindowTitle}', " +
                $"session={GetProcessSessionId(explorerWindow.ProcessId)}, elevated={FormatElevation(explorerWindow.IsElevated)}.");
            return explorerWindow;
        }

        Assert.Fail(
            $"Explorer did not open for {selectedItemName} after {ExplorerOpenAttempts} launch attempts." +
            Environment.NewLine + GetActivationDiagnostics("Explorer launch failed"));
        return null!;
    }

    private static bool WaitForExplorerWindowTitle(long windowHandle, string expectedTitle, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);

        while (DateTime.UtcNow < deadline)
        {
            var window = WindowControl.EnumerateAllWindows()
                .FirstOrDefault(candidate => candidate.Hwnd.ToInt64() == windowHandle);
            if (window.Hwnd != IntPtr.Zero &&
                window.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private void SetExplorerSelection(
        Session explorerWindow,
        IReadOnlyList<string> selectedPaths,
        string focusedPath)
    {
        var normalizedPaths = selectedPaths.Select(NormalizePath).ToList();
        var normalizedFocusedPath = NormalizePath(focusedPath);

        Assert.IsTrue(
            SetAndWaitForExplorerSelection(
                explorerWindow.WindowHandle,
                normalizedPaths,
                normalizedFocusedPath,
                ExplorerOpenTimeoutMS),
            $"Explorer did not establish the expected selection [{string.Join(", ", normalizedPaths.Select(Path.GetFileName))}] " +
            $"with '{Path.GetFileName(normalizedFocusedPath)}' focused.");

        explorerWindowHandle = explorerWindow.WindowHandle;
        expectedExplorerSelection = normalizedPaths;
        expectedExplorerFocusedPath = normalizedFocusedPath;
    }

    private static bool SetAndWaitForExplorerSelection(
        long windowHandle,
        IReadOnlyList<string> selectedPaths,
        string focusedPath,
        int timeoutMS)
    {
        var expectedPaths = selectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nativeWindowHandle = new IntPtr(windowHandle);
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        var stableSamples = 0;

        while (DateTime.UtcNow < deadline)
        {
            if (WindowControl.GetForegroundWindowHandle() != nativeWindowHandle)
            {
                stableSamples = 0;
                WindowControl.TryBringToForeground(nativeWindowHandle);
                Thread.Sleep(250);
                continue;
            }

            var snapshot = GetExplorerSelection(windowHandle);
            if (snapshot is not null &&
                snapshot.SelectedPaths.SetEquals(expectedPaths) &&
                string.Equals(snapshot.FocusedPath, focusedPath, StringComparison.OrdinalIgnoreCase))
            {
                stableSamples++;
                if (stableSamples >= ExplorerSelectionStableSamples)
                {
                    return true;
                }
            }
            else
            {
                stableSamples = 0;
                TrySetExplorerSelection(windowHandle, selectedPaths, focusedPath);
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static bool TrySetExplorerSelection(
        long windowHandle,
        IReadOnlyList<string> selectedPaths,
        string focusedPath)
    {
        object? shellObject = null;
        ShellWindows? shellWindows = null;

        try
        {
            var shellType = Type.GetTypeFromCLSID(ShellApplicationClassId, throwOnError: true)!;
            shellObject = Activator.CreateInstance(shellType);
            var shell = (Shell32.IShellDispatch2)shellObject!;
            shellWindows = shell.Windows();
            foreach (IWebBrowserApp browser in shellWindows)
            {
                try
                {
                    if (browser.HWND != windowHandle || browser.Document is not Shell32.IShellFolderViewDual2 folderView)
                    {
                        continue;
                    }

                    var folder = folderView.Folder;
                    var folderItems = folder.Items();
                    var retainedItems = new List<Shell32.FolderItem>();
                    try
                    {
                        var itemsByPath = new Dictionary<string, Shell32.FolderItem>(StringComparer.OrdinalIgnoreCase);
                        for (var index = 0; index < folderItems.Count; index++)
                        {
                            var item = folderItems.Item(index);
                            var normalizedPath = NormalizePath(item.Path);
                            if (selectedPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                            {
                                itemsByPath[normalizedPath] = item;
                                retainedItems.Add(item);
                            }
                            else
                            {
                                Marshal.ReleaseComObject(item);
                            }
                        }

                        if (itemsByPath.Count != selectedPaths.Count)
                        {
                            return false;
                        }

                        var orderedPaths = selectedPaths
                            .Where(path => !string.Equals(path, focusedPath, StringComparison.OrdinalIgnoreCase))
                            .Append(focusedPath)
                            .ToList();

                        for (var index = 0; index < orderedPaths.Count; index++)
                        {
                            var path = orderedPaths[index];
                            var flags = ShellViewSelect | ShellViewEnsureVisible;
                            if (index == 0)
                            {
                                flags |= ShellViewDeselectOthers;
                            }

                            if (string.Equals(path, focusedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                flags |= ShellViewFocused;
                            }

                            folderView.SelectItem(itemsByPath[path], flags);
                        }

                        return true;
                    }
                    finally
                    {
                        foreach (var item in retainedItems)
                        {
                            Marshal.ReleaseComObject(item);
                        }

                        Marshal.ReleaseComObject(folderItems);
                        Marshal.ReleaseComObject(folder);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(browser);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            if (shellWindows is not null)
            {
                Marshal.ReleaseComObject(shellWindows);
            }

            if (shellObject is not null)
            {
                Marshal.ReleaseComObject(shellObject);
            }
        }

        return false;
    }

    private static ExplorerSelectionSnapshot? GetExplorerSelection(long windowHandle)
    {
        object? shellObject = null;
        ShellWindows? shellWindows = null;

        try
        {
            var shellType = Type.GetTypeFromCLSID(ShellApplicationClassId, throwOnError: true)!;
            shellObject = Activator.CreateInstance(shellType);
            var shell = (Shell32.IShellDispatch2)shellObject!;
            shellWindows = shell.Windows();
            foreach (IWebBrowserApp browser in shellWindows)
            {
                try
                {
                    if (browser.HWND != windowHandle || browser.Document is not Shell32.IShellFolderViewDual2 folderView)
                    {
                        continue;
                    }

                    var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var selectedItems = folderView.SelectedItems();
                    try
                    {
                        for (var index = 0; index < selectedItems.Count; index++)
                        {
                            var item = selectedItems.Item(index);
                            try
                            {
                                selectedPaths.Add(NormalizePath(item.Path));
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(item);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(selectedItems);
                    }

                    var focusedItem = folderView.FocusedItem;
                    if (focusedItem is null)
                    {
                        return new ExplorerSelectionSnapshot(selectedPaths, null);
                    }

                    try
                    {
                        return new ExplorerSelectionSnapshot(selectedPaths, NormalizePath(focusedItem.Path));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(focusedItem);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(browser);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            if (shellWindows is not null)
            {
                Marshal.ReleaseComObject(shellWindows);
            }

            if (shellObject is not null)
            {
                Marshal.ReleaseComObject(shellObject);
            }
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar));
    }

    private Session SendPeekHotkeyWithRetry(string expectedPath)
    {
        for (var attempt = 1; attempt <= MaxHotkeyAttempts; attempt++)
        {
            var visiblePeekWindow = WindowsFinder.ListByApp(PeekProcessName).FirstOrDefault();
            if (visiblePeekWindow is not null)
            {
                return WaitForInitializedPeekWindow(
                    expectedPath,
                    visiblePeekWindow.Hwnd,
                    visiblePeekWindow.ProcessId,
                    visiblePeekWindow.Title,
                    ElevationHelper.IsProcessElevated(visiblePeekWindow.ProcessId),
                    attempt);
            }

            Assert.IsFalse(
                expectedExplorerSelection.Count == 0 || string.IsNullOrEmpty(expectedExplorerFocusedPath),
                "Explorer selection expectations were not initialized before Peek activation.");
            var explorerReady = explorerWindowHandle != 0 &&
                                SetAndWaitForExplorerSelection(
                    explorerWindowHandle,
                    expectedExplorerSelection,
                    expectedExplorerFocusedPath!,
                    ExplorerOpenTimeoutMS);
            Assert.IsTrue(
                explorerReady,
                $"Explorer did not become foreground with the expected stable selection before Peek hotkey attempt {attempt}.");

            TestContext.WriteLine(
                $"Peek hotkey attempt {attempt}/{MaxHotkeyAttempts} for '{Path.GetFileName(expectedPath)}': " +
                $"explorerHwnd={explorerWindowHandle}, explorerReady={explorerReady}, " +
                $"foregroundHwnd={WindowControl.GetForegroundWindowHandle().ToInt64()}.");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.Space);
            var appearedWindow = WindowsFinder.WaitForWindowByApp(
                PeekProcessName,
                _ => true,
                PeekWindowTimeoutMS);
            if (appearedWindow is not null)
            {
                return WaitForInitializedPeekWindow(
                    expectedPath,
                    appearedWindow.WindowHandle,
                    appearedWindow.ProcessId,
                    appearedWindow.WindowTitle,
                    appearedWindow.IsElevated,
                    attempt);
            }

            TestContext.WriteLine(GetActivationDiagnostics($"No matching Peek window after hotkey attempt {attempt}"));
        }

        Assert.Fail(
            $"Peek did not open {Path.GetFileName(expectedPath)} after {MaxHotkeyAttempts} hotkey attempts." +
            Environment.NewLine + GetActivationDiagnostics("Peek activation failed"));
        return null!;
    }

    private Session WaitForInitializedPeekWindow(
        string expectedPath,
        long windowHandle,
        int processId,
        string initialTitle,
        bool? isElevated,
        int hotkeyAttempt)
    {
        TestContext.WriteLine(
            $"Peek window appeared after hotkey attempt {hotkeyAttempt}: hwnd={windowHandle}, " +
            $"pid={processId}, initialTitle='{initialTitle}', " +
            $"session={GetProcessSessionId(processId)}, elevated={FormatElevation(isElevated)}. " +
            $"Waiting for expected title without resending the hotkey.");

        EnsurePeekWindowForeground(windowHandle);
        var initializedWindow = WaitForPeekWindow(expectedPath, PeekWindowTimeoutMS);
        if (initializedWindow is not null)
        {
            EnsurePeekWindowForeground(initializedWindow);
            TestContext.WriteLine(
                $"Peek initialized after hotkey attempt {hotkeyAttempt}: hwnd={initializedWindow.WindowHandle}, " +
                $"pid={initializedWindow.ProcessId}, title='{initializedWindow.WindowTitle}', " +
                $"session={GetProcessSessionId(initializedWindow.ProcessId)}, elevated={FormatElevation(initializedWindow.IsElevated)}.");
            return initializedWindow;
        }

        Assert.Fail(
            $"Peek window appeared after hotkey attempt {hotkeyAttempt}, but did not initialize for " +
            $"{Path.GetFileName(expectedPath)} within {PeekWindowTimeoutMS / 1_000}s." +
            Environment.NewLine + GetActivationDiagnostics("Peek window initialization failed"));
        return null!;
    }

    private string GetActivationDiagnostics(string stage)
    {
        var output = new StringBuilder();
        using var testHost = Process.GetCurrentProcess();
        output.AppendLine($"[{DateTime.UtcNow:O}] {stage}");
        output.AppendLine(
            $"Test host: pid={testHost.Id}, session={GetProcessSessionId(testHost.Id)}, " +
            $"elevated={ElevationHelper.IsCurrentProcessElevated()}, foregroundHwnd={WindowControl.GetForegroundWindowHandle().ToInt64()}.");
        output.AppendLine(
            $"Settings session: pid={Session.ProcessId}, session={GetProcessSessionId(Session.ProcessId)}, " +
            $"elevated={FormatElevation(Session.IsElevated)}.");
        output.AppendLine(
            $"Configured Peek shortcut: Ctrl+Space; AlwaysRunNotElevated=true; EnableSpaceToActivate=false; " +
            $"AppsUseLightTheme={ReadAppsUseLightTheme()?.ToString() ?? "unknown"}.");
        AppendProcessDiagnostics(output, "PowerToys");
        AppendProcessDiagnostics(output, "explorer");
        AppendProcessDiagnostics(output, PeekProcessName);
        AppendWindowDiagnostics(output, "explorer");
        AppendWindowDiagnostics(output, PeekProcessName);
        return output.ToString();
    }

    private static void AppendProcessDiagnostics(StringBuilder output, string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            output.AppendLine($"Process '{processName}': none.");
            return;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                output.AppendLine(
                    $"Process '{processName}': pid={process.Id}, session={GetProcessSessionId(process.Id)}, " +
                    $"elevated={FormatElevation(ElevationHelper.IsProcessElevated(process.Id))}, " +
                    $"mainHwnd={GetMainWindowHandle(process)}.");
            }
        }
    }

    private static void AppendWindowDiagnostics(StringBuilder output, string appName)
    {
        var windows = WindowsFinder.ListByApp(appName);
        if (windows.Count == 0)
        {
            output.AppendLine($"Windows for '{appName}': none.");
            return;
        }

        foreach (var window in windows)
        {
            output.AppendLine(
                $"Window for '{appName}': hwnd={window.Hwnd}, pid={window.ProcessId}, " +
                $"class='{window.ClassName}', title='{window.Title}', size={window.Width}x{window.Height}.");
        }
    }

    private static int? GetProcessSessionId(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.SessionId;
        }
        catch
        {
            return null;
        }
    }

    private static long? GetMainWindowHandle(Process process)
    {
        try
        {
            return process.MainWindowHandle.ToInt64();
        }
        catch
        {
            return null;
        }
    }

    private static string FormatElevation(bool? elevated) => elevated?.ToString() ?? "unknown";

    private static void ForcePipelineLightTheme()
    {
        if (!EnvironmentConfig.IsInPipeline)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizeRegistryPath);
            appsUseLightThemeValueExisted = key.GetValueNames()
                .Contains(AppsUseLightThemeValueName, StringComparer.OrdinalIgnoreCase);
            if (appsUseLightThemeValueExisted)
            {
                originalAppsUseLightThemeValue = key.GetValue(AppsUseLightThemeValueName);
                originalAppsUseLightThemeValueKind = key.GetValueKind(AppsUseLightThemeValueName);
            }

            key.SetValue(AppsUseLightThemeValueName, 1, RegistryValueKind.DWord);
            restoreAppsUseLightTheme = true;
        }
        catch
        {
        }
    }

    private static int? ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            return key?.GetValue(AppsUseLightThemeValueName) is int value ? value : null;
        }
        catch
        {
            return null;
        }
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

    private Session NavigateToFileWithRetry(Session peekWindow, Key key, string expectedPath)
    {
        for (var attempt = 1; attempt <= MaxNavigationAttempts; attempt++)
        {
            var expectedWindow = WaitForPeekWindow(expectedPath, timeoutMS: 500);
            if (expectedWindow is not null)
            {
                return expectedWindow;
            }

            EnsurePeekWindowInteractive(peekWindow);
            EnsurePeekWindowForeground(peekWindow);
            KeyboardHelper.SendKeys(key);

            expectedWindow = WaitForPeekWindow(expectedPath, PeekWindowTimeoutMS);
            if (expectedWindow is not null)
            {
                return expectedWindow;
            }
        }

        Assert.Fail(
            $"Peek did not navigate to {Path.GetFileName(expectedPath)} after " +
            $"{MaxNavigationAttempts} {key} key attempts.");
        return null!;
    }

    private void EnsurePeekWindowForeground(Session peekWindow)
    {
        EnsurePeekWindowForeground(peekWindow.WindowHandle);
    }

    private void EnsurePeekWindowForeground(long windowHandle)
    {
        var nativeWindowHandle = new IntPtr(windowHandle);
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(PeekWindowTimeoutMS);

        while (DateTime.UtcNow < deadline)
        {
            if (WindowControl.GetForegroundWindowHandle() == nativeWindowHandle)
            {
                return;
            }

            WindowControl.TryBringToForeground(nativeWindowHandle);
            Thread.Sleep(250);
        }

        Assert.Fail(
            $"Peek HWND {windowHandle} did not become the foreground window within " +
            $"{PeekWindowTimeoutMS / 1_000}s." + Environment.NewLine +
            GetActivationDiagnostics("Peek foreground activation failed"));
    }

    private static void EnsurePeekReady(Session peekWindow)
    {
        EnsurePeekWindowInteractive(peekWindow);

        var previewState = peekWindow.Find<Element>(By.AccessibilityId("PreviewStateAutomationPeer"), 15_000);
        Assert.IsTrue(
            previewState.WaitForValue("Loaded", timeoutMS: PreviewLoadTimeoutMS),
            $"Peek did not finish loading '{peekWindow.WindowTitle}' within {PreviewLoadTimeoutMS / 1_000}s. " +
            $"Last preview state: '{previewState.GetValue()}'.");

        var loadingIndicator = peekWindow
            .FindAll<Element>(By.AccessibilityId("LoadingIndicator"), 1_000)
            .FirstOrDefault();

        if (loadingIndicator is not null)
        {
            Assert.IsTrue(
                loadingIndicator.WaitForGone(PreviewLoadTimeoutMS),
                $"Peek's loading indicator did not disappear for '{peekWindow.WindowTitle}'.");
        }
    }

    private static void EnsurePeekWindowInteractive(Session peekWindow)
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

    private void CloseTestWindows(bool preservePeekProcess = true)
    {
        var peekClosed = WindowControl.TryCloseByApp(PeekProcessName, timeoutMS: 10_000);
        var peekSettled = preservePeekProcess ? WaitForPeekProcessInputIdle() : StopPeekProcess();
        var explorerClosed = CloseExplorerFileWindows();

        if (!peekClosed || !peekSettled)
        {
            TestContext.WriteLine(
                preservePeekProcess
                    ? "Cleanup could not close Peek and wait for its UI thread to become idle within 10 seconds."
                    : "Cleanup could not close and stop Peek within 10 seconds.");
        }

        if (!explorerClosed)
        {
            TestContext.WriteLine("Cleanup could not close every File Explorer window within 10 seconds.");
        }

        explorerWindowHandle = 0;
        expectedExplorerSelection = Array.Empty<string>();
        expectedExplorerFocusedPath = null;
    }

    private static bool WaitForPeekProcessInputIdle()
    {
        var processes = Process.GetProcessesByName(PeekProcessName);
        try
        {
            foreach (var process in processes)
            {
                if (!process.WaitForInputIdle(10_000))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool StopPeekProcess()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var processes = Process.GetProcessesByName(PeekProcessName);
            if (processes.Length == 0)
            {
                return true;
            }

            try
            {
                foreach (var process in processes)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }

                foreach (var process in processes)
                {
                    var remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                    if (!process.WaitForExit(remaining))
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }

    private static bool CloseExplorerFileWindows()
    {
        return WindowControl.TryCloseByApp("explorer", IsExplorerFileWindow, timeoutMS: 10_000);
    }

    private static bool IsExplorerFileWindow(WindowsFinder.WindowInfo window)
    {
        return string.Equals(window.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
    {
        public (int X, int Y) Center => (Left + (Width / 2), Top + (Height / 2));
    }

    private sealed record ExplorerSelectionSnapshot(HashSet<string> SelectedPaths, string? FocusedPath);
}
