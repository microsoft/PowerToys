// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.PreviewPane.UITests;

[TestClass]
[DoNotParallelize]
public class FileExplorerAddonsTests : UITestBase
{
    private const string PreviewHandlerShellExtension = "{8895b1c6-b41f-4c1c-a562-0d564250836f}";
    private const string ThumbnailHandlerShellExtension = "{e357fccd-a995-4576-b01f-234630154e96}";
    private const string ThumbnailIsolationRegistryPath = @"Software\Classes\CLSID\{E357FCCD-A995-4576-B01F-234630154E96}";
    private const string DisableProcessIsolationValueName = "DisableProcessIsolation";
    private const string EmptyPreviewPaneText = "Select a file to preview.";

    private const string MarkdownPreviewHandler = "{60789D87-9C3C-44AF-B18C-3DE2C2820ED3}";
    private const string SvgPreviewHandler = "{FCDD4EED-41AA-492F-8A84-31A1546226E0}";
    private const string PdfPreviewHandler = "{A5A41CC7-02CB-41D4-8C9B-9087040D6098}";
    private const string GcodePreviewHandler = "{A0257634-8812-4CE8-AF11-FA69ACAEAFAE}";
    private const string MonacoPreviewHandler = "{D8034CFA-F34B-41FE-AD45-62FCBB52A6DA}";

    private const string SvgThumbnailProvider = "{10144713-1526-46C9-88DA-1FB52807A9FF}";
    private const string PdfThumbnailProvider = "{D8BB9942-93BD-412D-87E4-33FAB214DC1A}";
    private const string GcodeThumbnailProvider = "{F2847CBE-CD03-4C83-A359-1A8052C1B9D5}";
    private const string StlThumbnailProvider = "{77257004-6F25-4521-B602-50ECC6EC62A6}";

    private const int ExplorerTimeoutMS = 30_000;
    private const int ExplorerOpenAttempts = 3;
    private const int PreviewPaneDetectionTimeoutMS = 2_000;
    private const int PreviewPaneOpenTimeoutMS = 10_000;
    private const int PreviewTimeoutMS = 60_000;
    private const int VisualStableTimeoutMS = 15_000;
    private const int ExtraLargeIconSize = 256;
    private const int LargeIconSize = 96;
    private const int MediumIconSize = 48;
    private const double PreviewRegionDifferenceThreshold = 0.75;
    private static readonly TimeSpan FailureRecordingTail = TimeSpan.FromSeconds(2);

    private static readonly string[] FileExplorerModule = { "File Explorer" };
    private static readonly (string Extension, string Clsid)[] ThumbnailProviders =
    {
        (".svg", SvgThumbnailProvider),
        (".pdf", PdfThumbnailProvider),
        (".gcode", GcodeThumbnailProvider),
        (".stl", StlThumbnailProvider),
    };

    private static readonly object ExplorerPreparationLock = new();
    private static readonly IDisposable FileExplorerSettings;
    private static List<SandboxThumbnailRegistration>? sandboxThumbnailRegistrations;
    private static bool explorerPrepared;

    private readonly List<string> temporaryFolders = new();
    private long explorerWindowHandle;

    public FileExplorerAddonsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: FileExplorerModule)
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    static FileExplorerAddonsTests()
    {
        FileExplorerSettings = SettingsConfigHelper.PreserveModuleSettings("File Explorer");
        try
        {
            SettingsConfigHelper.UpdateModuleSettings(
                "File Explorer",
                """
                {
                  "name": "File Explorer",
                  "version": "1.0",
                  "properties": {}
                }
                """,
                settings =>
                {
                    var properties = settings["properties"] as JsonObject ?? new JsonObject();
                    foreach (var settingName in new[]
                    {
                        "md-previewer-toggle-setting",
                        "svg-previewer-toggle-setting",
                        "pdf-previewer-toggle-setting",
                        "gcode-previewer-toggle-setting",
                        "monaco-previewer-toggle-setting",
                        "svg-thumbnail-toggle-setting",
                        "pdf-thumbnail-toggle-setting",
                        "gcode-thumbnail-toggle-setting",
                        "stl-thumbnail-toggle-setting",
                    })
                    {
                        properties[settingName] = new JsonObject { ["value"] = true };
                    }

                    settings["properties"] = properties;
                });
        }
        catch
        {
            FileExplorerSettings.Dispose();
            throw;
        }
    }

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
        using var process = Process.GetCurrentProcess();
        process.ProcessorAffinity = new IntPtr(1);
        Assert.AreEqual(new IntPtr(1), process.ProcessorAffinity, "PreviewPane.UITests must run on logical processor 0.");
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        try
        {
            if (sandboxThumbnailRegistrations is null)
            {
                return;
            }

            for (var index = sandboxThumbnailRegistrations.Count - 1; index >= 0; index--)
            {
                sandboxThumbnailRegistrations[index].Dispose();
            }

            sandboxThumbnailRegistrations = null;
        }
        finally
        {
            FileExplorerSettings.Dispose();
        }
    }

    [TestInitialize]
    public void PrepareTest()
    {
        CloseExplorerFileWindows();
    }

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(FailureRecordingTail);

        CloseExplorerFileWindows();
        explorerWindowHandle = 0;

        foreach (var folder in temporaryFolders)
        {
            DeleteDirectoryWithRetry(folder);
        }

        temporaryFolders.Clear();
    }

    [TestMethod("FileExplorerAddons.Preview.Markdown")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Preview Pane")]
    public void MarkdownPreviewShowsReadmeContent()
    {
        TestPreview(
            ".md",
            MarkdownPreviewHandler,
            "README.md",
            "markdown",
            "MarkdownPreviewHandler",
            "MDPrevHandler");
    }

    [TestMethod("FileExplorerAddons.Preview.SVG")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Preview Pane")]
    public void SvgPreviewShowsImageContent()
    {
        TestPreview(".svg", SvgPreviewHandler, "sample.svg", "svg", "SvgPreviewHandler", "SvgPrevHandler");
    }

    [TestMethod("FileExplorerAddons.Preview.PDF")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Preview Pane")]
    public void PdfPreviewShowsDocumentContent()
    {
        TestPreview(".pdf", PdfPreviewHandler, "sample.pdf", "pdf", "PdfPreviewHandler", "PdfPrevHandler");
    }

    [TestMethod("FileExplorerAddons.Preview.Gcode")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Preview Pane")]
    public void GcodePreviewShowsToolpathContent()
    {
        TestPreview(
            ".gcode",
            GcodePreviewHandler,
            "sample.gcode",
            "gcode",
            "GcodePreviewHandler",
            "GcodePreviewHandler");
    }

    [TestMethod("FileExplorerAddons.Preview.SourceCode")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Preview Pane")]
    public void MonacoPreviewShowsSyntaxHighlightedSource()
    {
        TestPreview(
            ".cpp",
            MonacoPreviewHandler,
            "main.cpp",
            "source-code",
            "MonacoPreviewHandler",
            "MonacoPrevHandler");
    }

    [TestMethod("FileExplorerAddons.Thumbnail.SVG")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Icon Preview")]
    public void SvgThumbnailRendersAtMultipleIconSizes()
    {
        TestThumbnail(
            ".svg",
            SvgThumbnailProvider,
            "sample.svg",
            "PowerToys.SvgThumbnailProvider",
            "svg");
    }

    [TestMethod("FileExplorerAddons.Thumbnail.PDF")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Icon Preview")]
    public void PdfThumbnailRendersAtMultipleIconSizes()
    {
        TestThumbnail(
            ".pdf",
            PdfThumbnailProvider,
            "sample.pdf",
            "PowerToys.PdfThumbnailProvider",
            "pdf");
    }

    [TestMethod("FileExplorerAddons.Thumbnail.Gcode")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Icon Preview")]
    public void GcodeThumbnailRendersAtMultipleIconSizes()
    {
        TestThumbnail(
            ".gcode",
            GcodeThumbnailProvider,
            "sample.gcode",
            "PowerToys.GcodeThumbnailProvider",
            "gcode");
    }

    [TestMethod("FileExplorerAddons.Thumbnail.STL")]
    [TestCategory("File Explorer Add-ons")]
    [TestCategory("Icon Preview")]
    public void StlThumbnailRendersAtMultipleIconSizes()
    {
        TestThumbnail(
            ".stl",
            StlThumbnailProvider,
            "sample.stl",
            "PowerToys.StlThumbnailProvider",
            "stl");
    }

    private void TestPreview(
        string extension,
        string expectedClsid,
        string assetName,
        string scenario,
        string handlerName,
        string handlerLogFolder)
    {
        AssertShellExtensionRegistration(extension, PreviewHandlerShellExtension, expectedClsid, "preview handler");
        PrepareExplorerForRegisteredHandlers();

        var filePath = TestAssetPath(assetName);
        var explorer = OpenExplorer(Path.GetDirectoryName(filePath)!);
        EnsurePreviewPaneOpen(explorer);
        var handlerLogDirectory = LocalLowHandlerLogDirectory(handlerLogFolder);
        DeleteDirectoryWithRetry(handlerLogDirectory);
        Assert.IsFalse(
            Directory.Exists(handlerLogDirectory),
            $"Could not clear the previous {handlerName} log before previewing {extension}.");

        var emptyPreviewPath = CaptureStableWindow(explorer, $"{scenario}-empty");
        SelectFile(explorer, filePath);

        var handlerLog = WaitForProviderLog(
            handlerLogDirectory,
            $"Starting {handlerName}.exe",
            ExplorerTimeoutMS);
        Assert.IsNotNull(
            handlerLog,
            $"Explorer rendered {extension}, but did not invoke the PowerToys {handlerName} shim.");
        var handlerLogText = ReadAllTextWithRetry(handlerLog!);
        Assert.IsFalse(
            handlerLogText.Contains("Failed to start", StringComparison.OrdinalIgnoreCase),
            $"The PowerToys {handlerName} shim reported a launch failure.{Environment.NewLine}{handlerLogText}");
        var persistedLogPath = ArtifactPath($"{scenario}-handler", ".log");
        File.WriteAllText(persistedLogPath, handlerLogText);
        var renderedPreviewPath = WaitForVisibleChange(explorer, emptyPreviewPath, $"{scenario}-rendered");

        TestContext.AddResultFile(emptyPreviewPath);
        TestContext.AddResultFile(renderedPreviewPath);
        TestContext.AddResultFile(persistedLogPath);
    }

    private void TestThumbnail(
        string extension,
        string expectedClsid,
        string assetName,
        string providerProcessName,
        string scenario)
    {
        AssertShellExtensionRegistration(extension, ThumbnailHandlerShellExtension, expectedClsid, "thumbnail provider");
        PrepareExplorerForRegisteredHandlers();

        var sourcePath = TestAssetPath(assetName);
        var testFolder = CreateTemporaryFolder();
        var destinationPath = Path.Combine(testFolder, assetName);
        var explorer = OpenExplorer(testFolder);
        var providerName = providerProcessName["PowerToys.".Length..];
        var providerLogDirectory = LocalLowHandlerLogDirectory(providerName);
        DeleteDirectoryWithRetry(providerLogDirectory);
        Assert.IsFalse(
            Directory.Exists(providerLogDirectory),
            $"Could not clear the previous {providerName} log before cold thumbnail generation.");

        File.Copy(sourcePath, destinationPath);
        KeyboardHelper.SendKeys(Key.F5);
        SelectFile(explorer, destinationPath);
        SetExplorerViewAndWait(
            explorer,
            assetName,
            ExtraLargeIconSize,
            "extra-large icons",
            minimumItemHeight: 180,
            maximumItemHeight: int.MaxValue);

        var providerLog = WaitForProviderLog(
            providerLogDirectory,
            $"Start {providerName}.exe",
            ExplorerTimeoutMS);
        Assert.IsNotNull(
            providerLog,
            $"Windows Shell did not invoke the PowerToys {providerName} shim for the cold {extension} thumbnail.");
        var providerLogText = ReadAllTextWithRetry(providerLog!);
        Assert.IsFalse(
            providerLogText.Contains("Bmp file not generated", StringComparison.OrdinalIgnoreCase) ||
            providerLogText.Contains("Failed to start", StringComparison.OrdinalIgnoreCase),
            $"The PowerToys {providerName} shim reported a generation failure.{Environment.NewLine}{providerLogText}");
        var persistedLogPath = ArtifactPath($"{scenario}-provider", ".log");
        File.WriteAllText(persistedLogPath, providerLogText);
        TestContext.AddResultFile(persistedLogPath);

        var extraLarge = CaptureStableFileItem(explorer, assetName, $"{scenario}-extra-large");

        SetExplorerViewAndWait(
            explorer,
            assetName,
            LargeIconSize,
            "large icons",
            minimumItemHeight: Math.Max(80, extraLarge.Height / 4),
            maximumItemHeight: extraLarge.Height * 7 / 10);
        var large = CaptureStableFileItem(explorer, assetName, $"{scenario}-large");
        SetExplorerViewAndWait(
            explorer,
            assetName,
            MediumIconSize,
            "medium icons",
            minimumItemHeight: large.Height / 2,
            maximumItemHeight: large.Height * 9 / 10);
        var medium = CaptureStableFileItem(explorer, assetName, $"{scenario}-medium");

        AssertThumbnailSizes(extraLarge, large, medium, extension);

        foreach (var capture in new[] { extraLarge, large, medium })
        {
            AssertImageHasVisualDetail(capture.Path, extension);
            TestContext.AddResultFile(capture.Path);
        }
    }

    private static void AssertShellExtensionRegistration(
        string extension,
        string shellExtension,
        string expectedClsid,
        string handlerDescription)
    {
        var registryPath = $@"{extension}\shellex\{shellExtension}";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        string? actualClsid = null;

        while (DateTime.UtcNow < deadline)
        {
            using var key = Registry.ClassesRoot.OpenSubKey(registryPath);
            actualClsid = key?.GetValue(null) as string;
            if (string.Equals(actualClsid, expectedClsid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Thread.Sleep(250);
        }

        Assert.Fail(
            $"PowerToys did not register the effective {extension} {handlerDescription}. " +
            $"Expected '{expectedClsid}' at HKCR\\{registryPath}; actual '{actualClsid ?? "<missing>"}'.");
    }

    private Session OpenExplorer(string folderPath)
    {
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
                Arguments = $"/n,\"{folderPath}\"",
                UseShellExecute = true,
            });

            var explorer = WindowsFinder.WaitForWindowByApp(
                "explorer",
                window => IsExplorerFileWindow(window) && !existingHandles.Contains(window.Hwnd),
                ExplorerTimeoutMS);
            if (explorer is null)
            {
                TestContext.WriteLine(
                    $"Explorer attempt {attempt}/{ExplorerOpenAttempts} did not create a fresh HWND for '{folderPath}'.");
                continue;
            }

            explorerWindowHandle = explorer.WindowHandle;
            if (WindowControl.WaitForForeground(
                    new IntPtr(explorerWindowHandle),
                    ExplorerTimeoutMS,
                    requiredConsecutiveMatches: 3))
            {
                return explorer;
            }

            TestContext.WriteLine(
                $"Explorer attempt {attempt}/{ExplorerOpenAttempts} HWND {explorerWindowHandle} did not become foreground. " +
                $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }

        Assert.Fail(
            $"Explorer did not open a stable foreground window for '{folderPath}' after {ExplorerOpenAttempts} attempts. " +
            $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return null!;
    }

    private static bool IsExplorerFileWindow(WindowsFinder.WindowInfo window)
    {
        return window.ClassName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CloseExplorerFileWindows()
    {
        return WindowControl.TryCloseByApp("explorer", IsExplorerFileWindow, timeoutMS: 10_000);
    }

    private static void RestartExplorerShell()
    {
        var oldProcesses = Process.GetProcessesByName("explorer");
        foreach (var process in oldProcesses)
        {
            try
            {
                process.Kill();
                process.WaitForExit(10_000);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        var shell = WindowsFinder.WaitForWindowByApp(
            "explorer",
            window => window.ClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 5_000);
        if (shell is null)
        {
            using var launchProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            });
            shell = WindowsFinder.WaitForWindowByApp(
                "explorer",
                window => window.ClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase),
                timeoutMS: ExplorerTimeoutMS);
        }

        Assert.IsNotNull(shell, "Explorer shell did not restart after File Explorer Add-ons registration.");
    }

    private static void PrepareExplorerForRegisteredHandlers()
    {
        lock (ExplorerPreparationLock)
        {
            if (explorerPrepared)
            {
                return;
            }

            if (Environment.UserName.Equals("WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase))
            {
                sandboxThumbnailRegistrations = new List<SandboxThumbnailRegistration>();
                try
                {
                    foreach (var (extension, clsid) in ThumbnailProviders)
                    {
                        AssertShellExtensionRegistration(
                            extension,
                            ThumbnailHandlerShellExtension,
                            clsid,
                            "thumbnail provider");
                        sandboxThumbnailRegistrations.Add(SandboxThumbnailRegistration.Create(extension, clsid));
                    }
                }
                catch
                {
                    for (var index = sandboxThumbnailRegistrations.Count - 1; index >= 0; index--)
                    {
                        sandboxThumbnailRegistrations[index].Dispose();
                    }

                    sandboxThumbnailRegistrations = null;
                    throw;
                }
            }

            RestartExplorerShell();
            explorerPrepared = true;
        }
    }

    private void EnsurePreviewPaneOpen(Session explorer)
    {
        EnsureExplorerForeground(explorer);
        if (WaitForPreviewPane(explorer, PreviewPaneDetectionTimeoutMS))
        {
            TestContext.WriteLine("Explorer's Preview pane was already open.");
            return;
        }

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            EnsureExplorerForeground(explorer);
            KeyboardHelper.SendKeys(Key.Alt, Key.P);
            if (WaitForPreviewPane(explorer, PreviewPaneOpenTimeoutMS))
            {
                TestContext.WriteLine($"Opened Explorer's Preview pane with Alt+P on attempt {attempt}.");
                return;
            }

            TestContext.WriteLine($"Explorer's Preview pane was not visible after Alt+P attempt {attempt}.");
        }

        Assert.Fail("Explorer's Preview pane did not open after two Alt+P attempts.");
    }

    private static bool WaitForPreviewPane(Session explorer, int timeoutMS)
    {
        return explorer.WaitFor(
            () => explorer.FindAll<Element>(By.Name(EmptyPreviewPaneText), timeoutMS: 250)
                .Any(element => element.Width > 0 && element.Height > 0),
            timeoutMS,
            pollIntervalMS: 250);
    }

    private static void EnsureExplorerForeground(Session explorer)
    {
        Assert.IsTrue(
            WindowControl.WaitForForeground(
                new IntPtr(explorer.WindowHandle),
                ExplorerTimeoutMS,
                requiredConsecutiveMatches: 3),
            $"Explorer HWND {explorer.WindowHandle} was not the stable foreground window.");
    }

    private static void SelectFile(Session explorer, string filePath)
    {
        var selection = ExplorerShell.SetSelectionAndWaitForStable(
            new IntPtr(explorer.WindowHandle),
            new[] { filePath },
            filePath,
            ExplorerTimeoutMS,
            requiredConsecutiveMatches: 4);

        Assert.IsTrue(
            selection.Succeeded,
            $"Explorer did not establish a stable selection for '{filePath}'. " +
            $"Last focused path: '{selection.LastObservation?.FocusedPath ?? "<none>"}'.");
    }

    private void SetExplorerViewAndWait(
        Session explorer,
        string fileName,
        int iconSize,
        string viewName,
        int minimumItemHeight,
        int maximumItemHeight)
    {
        Element? lastItem = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var view = ExplorerShell.SetViewModeAndIconSizeAndWait(
                new IntPtr(explorer.WindowHandle),
                ExplorerShell.ViewMode.Icons,
                iconSize,
                timeoutMS: 5_000);
            if (!view.Succeeded)
            {
                TestContext.WriteLine(
                    $"Explorer did not report {viewName} on attempt {attempt}; " +
                    $"last Shell view: {view.LastObservation?.Mode}, icon size: {view.LastObservation?.IconSize}.");
                continue;
            }

            var applied = explorer.WaitFor(
                () =>
                {
                    lastItem = FindVisibleFileItem(explorer, fileName, timeoutMS: 250);
                    return lastItem is not null &&
                           lastItem.Height >= minimumItemHeight &&
                           lastItem.Height <= maximumItemHeight;
                },
                timeoutMS: 5_000,
                pollIntervalMS: 250);
            if (applied)
            {
                TestContext.WriteLine(
                    $"Explorer applied {viewName} ({iconSize}px) on attempt {attempt}; item bounds: " +
                    $"{lastItem!.Width}x{lastItem.Height}.");
                return;
            }
        }

        Assert.Fail(
            $"Explorer did not apply {viewName} after three shortcut attempts. " +
            $"Expected item height {minimumItemHeight}..{maximumItemHeight}; " +
            $"last bounds: {lastItem?.Width ?? 0}x{lastItem?.Height ?? 0}.");
    }

    private string CaptureStableWindow(Session explorer, string name)
    {
        var previousPath = CaptureWindow(explorer, $"{name}-initial");
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(400);
            var currentPath = CaptureWindow(explorer, name);
            var difference = CalculateImageDifference(previousPath, currentPath, startXPercent: 0, startYPercent: 0);
            if (difference < 0.25)
            {
                File.Delete(previousPath);
                return currentPath;
            }

            File.Delete(previousPath);
            previousPath = currentPath;
        }

        return previousPath;
    }

    private string WaitForVisibleChange(Session explorer, string baselinePath, string name)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(PreviewTimeoutMS);
        var lastDifference = 0d;
        string? lastPath = null;

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            var currentPath = CaptureWindow(explorer, name);
            lastDifference = CalculateImageDifference(
                baselinePath,
                currentPath,
                startXPercent: 55,
                startYPercent: 18);
            TestContext.WriteLine($"Preview-region pixel change: {lastDifference:F2}%.");

            if (lastDifference >= PreviewRegionDifferenceThreshold)
            {
                if (lastPath is not null)
                {
                    File.Delete(lastPath);
                }

                return currentPath;
            }

            if (lastPath is not null)
            {
                File.Delete(lastPath);
            }

            lastPath = currentPath;
        }

        TestContext.AddResultFile(baselinePath);
        if (lastPath is not null)
        {
            TestContext.AddResultFile(lastPath);
        }

        Assert.Fail(
            $"The Explorer preview region did not visibly render within {PreviewTimeoutMS / 1_000}s. " +
            $"Expected at least {PreviewRegionDifferenceThreshold:F2}%; last sampled change was {lastDifference:F2}%.");
        return null!;
    }

    private ThumbnailCapture CaptureStableFileItem(Session explorer, string fileName, string name)
    {
        var previous = CaptureFileItem(explorer, fileName, $"{name}-initial");
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(VisualStableTimeoutMS);

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(400);
            var current = CaptureFileItem(explorer, fileName, name);
            var difference = CalculateImageDifference(previous.Path, current.Path, 0, 0, requireSameSize: false);
            if (previous.Width == current.Width &&
                previous.Height == current.Height &&
                difference < 0.25)
            {
                File.Delete(previous.Path);
                return current;
            }

            File.Delete(previous.Path);
            previous = current;
        }

        return previous;
    }

    private ThumbnailCapture CaptureFileItem(Session explorer, string fileName, string name)
    {
        var item = FindVisibleFileItem(explorer, fileName, timeoutMS: 5_000);
        Assert.IsNotNull(item, $"Explorer did not expose a visible item for '{fileName}'.");
        item!.ScrollIntoView();
        item = FindVisibleFileItem(explorer, fileName, timeoutMS: 5_000);
        Assert.IsNotNull(item, $"Explorer did not expose '{fileName}' after scrolling it into view.");

        var path = ArtifactPath(name);
        EnsureExplorerForeground(explorer);
        using (var bitmap = new Bitmap(item!.Width, item.Height))
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(item.X, item.Y, 0, 0, bitmap.Size);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        return new ThumbnailCapture(path, item.Width, item.Height);
    }

    private static Element? FindVisibleFileItem(Session explorer, string fileName, int timeoutMS)
    {
        var displayName = Path.GetFileNameWithoutExtension(fileName);
        return explorer.FindAll<Element>(By.Name(displayName), timeoutMS)
            .Where(element => element.Width > 0 && element.Height > 0)
            .Where(element =>
                element.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                element.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(element => element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(element => element.Width * element.Height)
            .FirstOrDefault();
    }

    private string CaptureWindow(Session explorer, string name)
    {
        var path = ArtifactPath(name);
        explorer.ScreenshotVisibleWindow(path);
        return path;
    }

    private string ArtifactPath(string name, string extension = ".png")
    {
        var currentTestName = TestContext.TestName ?? "unknown-test";
        var testName = string.Concat(
            currentTestName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var directory = Path.Combine(
            FindStableResultsRoot(),
            "FileExplorerAddons",
            testName);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    private string FindStableResultsRoot()
    {
        var candidate = TestContext.TestResultsDirectory ?? TestContext.TestRunResultsDirectory;
        var directory = string.IsNullOrWhiteSpace(candidate) ? null : new DirectoryInfo(candidate);
        while (directory is not null)
        {
            if (directory.Name.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetTempPath();
    }

    private static double CalculateImageDifference(
        string baselinePath,
        string currentPath,
        int startXPercent,
        int startYPercent,
        bool requireSameSize = true)
    {
        using var baseline = new Bitmap(baselinePath);
        using var current = new Bitmap(currentPath);
        if (baseline.Size != current.Size)
        {
            if (requireSameSize)
            {
                Assert.Fail("Explorer changed size while waiting for visual content.");
            }

            return 100;
        }

        var changedSamples = 0;
        var totalSamples = 0;
        var startX = baseline.Width * startXPercent / 100;
        var startY = baseline.Height * startYPercent / 100;

        for (var y = startY; y < baseline.Height; y += 3)
        {
            for (var x = startX; x < baseline.Width; x += 3)
            {
                var before = baseline.GetPixel(x, y);
                var after = current.GetPixel(x, y);
                var colorDelta = Math.Abs(before.R - after.R) +
                                 Math.Abs(before.G - after.G) +
                                 Math.Abs(before.B - after.B);
                if (colorDelta >= 45)
                {
                    changedSamples++;
                }

                totalSamples++;
            }
        }

        return totalSamples == 0 ? 0 : changedSamples * 100d / totalSamples;
    }

    private static void AssertThumbnailSizes(
        ThumbnailCapture extraLarge,
        ThumbnailCapture large,
        ThumbnailCapture medium,
        string extension)
    {
        Assert.IsTrue(
            extraLarge.Height >= 180 &&
            extraLarge.Height > large.Height &&
            large.Height > medium.Height,
            $"Explorer did not apply descending icon sizes for {extension}. " +
            $"Extra large: {extraLarge.Width}x{extraLarge.Height}; " +
            $"large: {large.Width}x{large.Height}; medium: {medium.Width}x{medium.Height}.");
    }

    private static void AssertImageHasVisualDetail(string imagePath, string extension)
    {
        using var image = new Bitmap(imagePath);
        var colorBuckets = new HashSet<int>();

        for (var y = 0; y < image.Height; y += 3)
        {
            for (var x = 0; x < image.Width; x += 3)
            {
                var color = image.GetPixel(x, y);
                colorBuckets.Add(((color.R >> 5) << 6) | ((color.G >> 5) << 3) | (color.B >> 5));
            }
        }

        Assert.IsTrue(
            colorBuckets.Count >= 6,
            $"The captured {extension} Explorer item has only {colorBuckets.Count} sampled color buckets; " +
            "the thumbnail appears blank or generic.");
    }

    private static string? WaitForProviderLog(string logDirectory, string expectedText, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var path in Directory.Exists(logDirectory)
                         ? Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                         : Array.Empty<string>())
            {
                var contents = ReadAllTextWithRetry(path);
                if (contents.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            Thread.Sleep(100);
        }

        return null;
    }

    private static string LocalLowHandlerLogDirectory(string handlerFolder)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "Microsoft",
            "PowerToys",
            "logs",
            "FileExplorer_localLow",
            handlerFolder);
    }

    private static string ReadAllTextWithRetry(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
        }

        return string.Empty;
    }

    private string TestAssetPath(string assetName)
    {
        var path = Path.GetFullPath(Path.Combine("TestAssets", assetName));
        Assert.IsTrue(File.Exists(path), $"File Explorer Add-ons test asset does not exist: {path}");
        return path;
    }

    private string CreateTemporaryFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PowerToys-FileExplorerAddons", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        temporaryFolders.Add(folder);
        return folder;
    }

    private static void DeleteDirectoryWithRetry(string folder)
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(folder); attempt++)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class SandboxThumbnailRegistration : IDisposable
    {
        private readonly bool active;
        private readonly string providerPath = string.Empty;
        private readonly string associationPath = string.Empty;
        private readonly RegistryTreeSnapshot? userProvider;
        private readonly RegistryTreeSnapshot? userAssociation;
        private readonly RegistryTreeSnapshot? machineProvider;
        private readonly RegistryTreeSnapshot? machineAssociation;
        private readonly RegistryTreeSnapshot? machineIsolation;

        private SandboxThumbnailRegistration()
        {
        }

        private SandboxThumbnailRegistration(
            string providerPath,
            string associationPath,
            RegistryTreeSnapshot userProvider,
            RegistryTreeSnapshot userAssociation,
            RegistryTreeSnapshot machineProvider,
            RegistryTreeSnapshot machineAssociation,
            RegistryTreeSnapshot machineIsolation)
        {
            active = true;
            this.providerPath = providerPath;
            this.associationPath = associationPath;
            this.userProvider = userProvider;
            this.userAssociation = userAssociation;
            this.machineProvider = machineProvider;
            this.machineAssociation = machineAssociation;
            this.machineIsolation = machineIsolation;
        }

        public static SandboxThumbnailRegistration Create(string extension, string providerClsid)
        {
            if (!Environment.UserName.Equals("WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase))
            {
                return new SandboxThumbnailRegistration();
            }

            var providerPath = $@"Software\Classes\CLSID\{providerClsid}";
            var associationPath = $@"Software\Classes\{extension}\shellex\{ThumbnailHandlerShellExtension}";
            var userProvider = RegistryTreeSnapshot.Capture(Registry.CurrentUser, providerPath);
            var userAssociation = RegistryTreeSnapshot.Capture(Registry.CurrentUser, associationPath);
            var machineProvider = RegistryTreeSnapshot.Capture(Registry.LocalMachine, providerPath);
            var machineAssociation = RegistryTreeSnapshot.Capture(Registry.LocalMachine, associationPath);
            var machineIsolation = RegistryTreeSnapshot.Capture(Registry.LocalMachine, ThumbnailIsolationRegistryPath);

            Assert.IsTrue(userProvider.Exists, $"Sandbox bridge could not find HKCU\\{providerPath}.");
            Assert.IsTrue(userAssociation.Exists, $"Sandbox bridge could not find HKCU\\{associationPath}.");

            try
            {
                userProvider.Restore(Registry.LocalMachine, providerPath);
                userAssociation.Restore(Registry.LocalMachine, associationPath);
                using (var isolationKey = Registry.LocalMachine.CreateSubKey(ThumbnailIsolationRegistryPath, writable: true))
                {
                    Assert.IsNotNull(isolationKey, "Could not create the machine Shell thumbnail isolation key in Sandbox.");
                    isolationKey!.SetValue(DisableProcessIsolationValueName, 1, RegistryValueKind.DWord);
                }

                Registry.CurrentUser.DeleteSubKeyTree(providerPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(associationPath, throwOnMissingSubKey: false);
                NotifyShellAssociationsChanged();

                return new SandboxThumbnailRegistration(
                    providerPath,
                    associationPath,
                    userProvider,
                    userAssociation,
                    machineProvider,
                    machineAssociation,
                    machineIsolation);
            }
            catch
            {
                machineIsolation.Restore(Registry.LocalMachine, ThumbnailIsolationRegistryPath);
                machineAssociation.Restore(Registry.LocalMachine, associationPath);
                machineProvider.Restore(Registry.LocalMachine, providerPath);
                userAssociation.Restore(Registry.CurrentUser, associationPath);
                userProvider.Restore(Registry.CurrentUser, providerPath);
                NotifyShellAssociationsChanged();
                throw;
            }
        }

        public void Dispose()
        {
            if (!active)
            {
                return;
            }

            machineIsolation!.Restore(Registry.LocalMachine, ThumbnailIsolationRegistryPath);
            machineAssociation!.Restore(Registry.LocalMachine, associationPath);
            machineProvider!.Restore(Registry.LocalMachine, providerPath);
            userAssociation!.Restore(Registry.CurrentUser, associationPath);
            userProvider!.Restore(Registry.CurrentUser, providerPath);
            NotifyShellAssociationsChanged();
        }
    }

    private sealed class RegistryTreeSnapshot
    {
        private readonly List<RegistryValueSnapshot> values = new();
        private readonly Dictionary<string, RegistryTreeSnapshot> subKeys = new(StringComparer.OrdinalIgnoreCase);

        private RegistryTreeSnapshot(bool exists)
        {
            Exists = exists;
        }

        public bool Exists { get; }

        public static RegistryTreeSnapshot Capture(RegistryKey root, string path)
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key is null ? new RegistryTreeSnapshot(false) : CaptureKey(key);
        }

        public void Restore(RegistryKey root, string path)
        {
            root.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            if (!Exists)
            {
                return;
            }

            using var key = root.CreateSubKey(path, writable: true) ??
                            throw new InvalidOperationException($"Could not restore registry key '{path}'.");
            RestoreKey(key);
        }

        private static RegistryTreeSnapshot CaptureKey(RegistryKey key)
        {
            var snapshot = new RegistryTreeSnapshot(true);
            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value is not null)
                {
                    snapshot.values.Add(new RegistryValueSnapshot(valueName, value, key.GetValueKind(valueName)));
                }
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName, writable: false);
                if (subKey is not null)
                {
                    snapshot.subKeys[subKeyName] = CaptureKey(subKey);
                }
            }

            return snapshot;
        }

        private void RestoreKey(RegistryKey key)
        {
            foreach (var value in values)
            {
                key.SetValue(value.Name, value.Value, value.Kind);
            }

            foreach (var (subKeyName, snapshot) in subKeys)
            {
                using var subKey = key.CreateSubKey(subKeyName, writable: true) ??
                                   throw new InvalidOperationException($"Could not restore registry subkey '{subKeyName}'.");
                snapshot.RestoreKey(subKey);
            }
        }
    }

    private sealed record RegistryValueSnapshot(string Name, object Value, RegistryValueKind Kind);

    private static void NotifyShellAssociationsChanged()
    {
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(long eventId, uint flags, IntPtr item1, IntPtr item2);

    private sealed record ThumbnailCapture(string Path, int Width, int Height);
}
