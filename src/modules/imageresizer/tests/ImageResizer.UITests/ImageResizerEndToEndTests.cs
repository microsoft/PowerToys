// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ImageResizer.UITests;

[TestClass]
[DoNotParallelize]
public sealed class ImageResizerEndToEndTests : UITestBase
{
    private const string ClassicContextMenuClassName = "#32768";
    private const string ContextMenuCaption = "Resize with Image Resizer";
    private const string ExplorerProcessName = "explorer";
    private const string ImageResizerModuleName = "Image Resizer";
    private const string ImageResizerProcessName = "PowerToys.ImageResizer";
    private const string ModernPackageName = "ImageResizerContextMenu";
    private const string ModernContextMenuClassName = "Microsoft.UI.Content.PopupWindowSiteBridge";
    private const int DialogTimeoutMS = 30_000;
    private const int ExplorerTimeoutMS = 30_000;
    private const int ResizeTimeoutMS = 60_000;
    private static readonly string[] ImageResizerModule = { ImageResizerModuleName };
    private static readonly ResizePreset DefaultPreset = new("UITest", ResizeFitMode.Fit, 100, 100, ResizeUnitMode.Pixel);
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly string ImageResizerSettingsPath = Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        ImageResizerModuleName,
        "settings.json");

    private static readonly string ImageResizerSizesPath = Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        ImageResizerModuleName,
        "sizes.json");

    private static bool originalSettingsFileExisted;
    private static string? originalSettingsContent;
    private static bool originalSizesFileExisted;
    private static string? originalSizesContent;
    private static bool contextMenuExplorerRefreshed;

    private readonly List<string> temporaryFolders = new();
    private long explorerWindowHandle;

    public ImageResizerEndToEndTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ImageResizerModule)
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
        originalSettingsFileExisted = File.Exists(ImageResizerSettingsPath);
        originalSettingsContent = originalSettingsFileExisted ? File.ReadAllText(ImageResizerSettingsPath) : null;
        originalSizesFileExisted = File.Exists(ImageResizerSizesPath);
        originalSizesContent = originalSizesFileExisted ? File.ReadAllText(ImageResizerSizesPath) : null;
        ConfigureResizeSettings(DefaultPreset);
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        TryRestoreSettingsFile(ImageResizerSizesPath, originalSizesFileExisted, originalSizesContent);
        TryRestoreSettingsFile(ImageResizerSettingsPath, originalSettingsFileExisted, originalSettingsContent);
    }

    [TestInitialize]
    public void PrepareTest()
    {
        Assert.IsTrue(CloseImageResizerWindows(), "A stale Image Resizer process could not be closed before the test.");
        Assert.IsTrue(CloseExplorerFileWindows(), "Stale Explorer file windows could not be closed before the test.");
    }

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        CloseImageResizerWindows();
        CloseExplorerFileWindows();
        explorerWindowHandle = 0;
        ConfigureResizeSettings(DefaultPreset);

        foreach (var folder in temporaryFolders)
        {
            if (!DeleteDirectoryWithRetry(folder))
            {
                TestContext.WriteLine($"Cleanup could not delete temporary folder '{folder}'.");
            }
        }

        temporaryFolders.Clear();
    }

    [TestMethod("ImageResizer.ContextMenu.EnabledState")]
    [TestCategory("Image Resizer")]
    public void ContextMenuTracksModuleEnabledState()
    {
        var settings = NavigateToImageResizerSettings();
        var toggle = settings.Find<ToggleSwitch>(By.Name("Image Resizer"));
        Assert.IsTrue(toggle.IsOn, "Image Resizer did not start from the deterministic enabled baseline.");
        var fixture = CreateImageFixture("context-menu.png", 400, 200);
        var folder = Path.GetDirectoryName(fixture)!;

        try
        {
            toggle = SetModuleEnabled(toggle, false);
            var explorer = OpenExplorer(folder);

            // Assert the real per-OS surface (modern tier-1 on Windows 11, classic on Windows 10)
            // with no classic fallback on Windows 11 — CI signs the sparse package so it registers.
            AssertContextMenuPresence(explorer, new[] { fixture }, expected: false);

            toggle = SetModuleEnabled(toggle, true);
            Assert.IsTrue(
                WaitForModernPackageRegistration(timeoutMS: 30_000),
                "The signed Image Resizer sparse package did not finish registering after the module was re-enabled.");
            contextMenuExplorerRefreshed = false;
            explorer = OpenExplorer(folder);
            AssertContextMenuPresence(explorer, new[] { fixture }, expected: true);
        }
        finally
        {
            try
            {
                SetModuleEnabled(toggle, true);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Restoring the Image Resizer toggle failed; restarting the deterministic scope. {ex.Message}");
                RestartScope(ImageResizerModule);
            }
        }
    }

    [TestMethod("ImageResizer.Settings.CustomPreset")]
    [TestCategory("Image Resizer")]
    public void RemovedAndAddedPresetsPopulateResizeWindow()
    {
        var removablePreset = new ResizePreset("Remove Me", ResizeFitMode.Fit, 320, 200, ResizeUnitMode.Pixel);
        var retainedPreset = new ResizePreset("Keep Me", ResizeFitMode.Fill, 200, 120, ResizeUnitMode.Pixel);
        ConfigureResizeSettings(new[] { removablePreset, retainedPreset });
        RestartScope(ImageResizerModule);
        var settings = NavigateToImageResizerSettings();

        var removeButton = FindExact<Button>(settings, "Remove the Remove Me preset");
        Assert.IsNotNull(removeButton, "The removable preset was not shown in Image Resizer settings.");
        removeButton!.Click();

        // The confirmation dialog can swallow the first click before its button is hit-testable, so
        // re-press Yes on every poll until the preset is actually gone.
        var settingsProcess = Session.FromProcess("PowerToys.Settings");
        Assert.IsTrue(
            settings.WaitFor(
                () =>
                {
                    FindExact<Button>(settingsProcess, "Yes", timeoutMS: 500)?.Click();
                    return FindExact<Button>(settings, "Remove the Remove Me preset", timeoutMS: 250) is null;
                },
                timeoutMS: 15_000,
                pollIntervalMS: 500),
            "The preset remained visible after confirming its removal.");

        settings.Find<Button>(By.AccessibilityId("AddSizeButton")).Click();
        var editNewPreset = FindExact<Button>(settings, "Edit the New size 1 preset");
        Assert.IsNotNull(editNewPreset, "Adding a preset did not create 'New size 1'.");
        editNewPreset!.Click(msPostAction: 500);

        // The expander toggle can miss its hit-test, leaving the editor collapsed; re-open it (the
        // pencil is only present while collapsed) until its Name field is exposed.
        settingsProcess = Session.FromProcess("PowerToys.Settings");
        var nameBox = FindExact<TextBox>(settingsProcess, "Name", timeoutMS: 2_000);
        for (var attempt = 0; nameBox is null && attempt < 5; attempt++)
        {
            FindExact<Button>(settings, "Edit the New size 1 preset", timeoutMS: 1_000)?.Click(msPostAction: 750);
            nameBox = FindExact<TextBox>(settingsProcess, "Name", timeoutMS: 2_000);
        }

        Assert.IsNotNull(nameBox, "The new preset editor did not expose its Name field.");
        nameBox!.SetText("UITest Custom");
        KeyboardHelper.SendKeys(Key.Esc);

        Assert.IsTrue(
            settings.WaitFor(
                () => FindExact<Button>(settings, "Edit the UITest Custom preset", timeoutMS: 250) is not null,
                timeoutMS: 5_000,
                pollIntervalMS: 250),
            "The renamed custom preset was not persisted in Settings.");

        var fixture = CreateImageFixture("preset.png", 400, 200);
        var dialog = OpenResizeDialog(fixture);
        var dialogProcess = Session.FromProcess(ImageResizerProcessName);
        dialogProcess.Find<ComboBox>(By.AccessibilityId("SizeComboBox")).Click(msPostAction: 300);

        Assert.IsNotNull(
            FindExact<Element>(dialogProcess, "UITest Custom"),
            "The newly added preset was not populated in the Image Resizer window.");
        Assert.IsNull(
            FindExact<Element>(dialogProcess, "Remove Me", timeoutMS: 500),
            "The removed preset was still populated in the Image Resizer window.");
        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsTrue(dialog.Has(By.AccessibilityId("SizeComboBox")), "The Image Resizer window closed unexpectedly.");
    }

    [TestMethod("ImageResizer.Resize.SingleAndMultiple")]
    [TestCategory("Image Resizer")]
    public void ResizesSingleAndMultipleImages()
    {
        ConfigureResizeSettings(DefaultPreset);

        var singleFolder = CreateTestFolder();
        var single = CreateImageFixture(singleFolder, "single.png", 400, 200);
        ResizeFiles(single);
        var singleOutput = WaitForResizedCopies(new[] { single }, expectedCount: 1).Single();
        AssertImageDimensions(singleOutput, 100, 50);

        var multipleFolder = CreateTestFolder();
        var landscape = CreateImageFixture(multipleFolder, "landscape.png", 400, 200);
        var portrait = CreateImageFixture(multipleFolder, "portrait.png", 200, 400);
        ResizeFiles(landscape, portrait);
        var multipleOutputs = WaitForResizedCopies(new[] { landscape, portrait }, expectedCount: 2);

        AssertImageDimensions(
            multipleOutputs.Single(path => Path.GetFileName(path).StartsWith("landscape", StringComparison.OrdinalIgnoreCase)),
            100,
            50);
        AssertImageDimensions(
            multipleOutputs.Single(path => Path.GetFileName(path).StartsWith("portrait", StringComparison.OrdinalIgnoreCase)),
            50,
            100);
    }

    [TestMethod("ImageResizer.Resize.GifWarning")]
    [TestCategory("Image Resizer")]
    public void GifSelectionShowsAnimationWarning()
    {
        ConfigureResizeSettings(DefaultPreset);
        var gif = CreateImageFixture("animated.gif", 200, 100);
        var dialog = OpenResizeDialog(gif);

        const string warning = "Gif files with animations may not be correctly resized.";
        Assert.IsNotNull(
            FindExact<Element>(dialog, warning),
            $"The Image Resizer window did not show the expected GIF warning: '{warning}'.");
    }

    [TestMethod("ImageResizer.Resize.FitModes")]
    [TestCategory("Image Resizer")]
    [DataRow("Fill", 0, 100, 100)]
    [DataRow("Fit", 1, 100, 50)]
    [DataRow("Stretch", 2, 100, 100)]
    public void ResizesImagesWithEveryFitMode(string modeName, int fitValue, int expectedWidth, int expectedHeight)
    {
        var preset = new ResizePreset(modeName, (ResizeFitMode)fitValue, 100, 100, ResizeUnitMode.Pixel);
        ConfigureResizeSettings(preset);
        var folder = CreateTestFolder();
        var source = CreateStripedImageFixture(folder, $"{modeName.ToLowerInvariant()}.png", 400, 200);

        ResizeFiles(source);
        var output = WaitForResizedCopies(new[] { source }, expectedCount: 1).Single();
        AssertImageDimensions(output, expectedWidth, expectedHeight);

        if ((ResizeFitMode)fitValue == ResizeFitMode.Fill)
        {
            AssertPixelDominatedBy(output, 10, 50, ColorChannel.Green);
            AssertPixelDominatedBy(output, 90, 50, ColorChannel.Green);
        }
        else if ((ResizeFitMode)fitValue == ResizeFitMode.Stretch)
        {
            AssertPixelDominatedBy(output, 10, 50, ColorChannel.Red);
            AssertPixelDominatedBy(output, 90, 50, ColorChannel.Blue);
        }
    }

    [TestMethod("ImageResizer.Resize.Units")]
    [TestCategory("Image Resizer")]
    [DataRow("Centimeters", 0, 2.54, 2.54, 96, 96)]
    [DataRow("Inches", 1, 1.0, 1.0, 96, 96)]
    [DataRow("Percent", 2, 50.0, 50.0, 200, 100)]
    [DataRow("Pixels", 3, 120.0, 80.0, 120, 80)]
    public void ResizesImagesUsingEveryDimensionUnit(
        string unitName,
        int unitValue,
        double width,
        double height,
        int expectedWidth,
        int expectedHeight)
    {
        var preset = new ResizePreset(unitName, ResizeFitMode.Stretch, width, height, (ResizeUnitMode)unitValue);
        ConfigureResizeSettings(preset);
        var source = CreateImageFixture($"{unitName.ToLowerInvariant()}.png", 400, 200);

        ResizeFiles(source);
        var output = WaitForResizedCopies(new[] { source }, expectedCount: 1).Single();
        AssertImageDimensions(output, expectedWidth, expectedHeight);
    }

    [TestMethod("ImageResizer.Resize.FilenameFormat")]
    [TestCategory("Image Resizer")]
    public void AppliesFilenameFormatToResizedImage()
    {
        const string format = "%1 - %2 - %3 - %4 - %5 - %6";
        var preset = new ResizePreset("Format", ResizeFitMode.Fit, 100, 100, ResizeUnitMode.Pixel);
        ConfigureResizeSettings(preset, fileNameFormat: format);
        var source = CreateImageFixture("format.png", 400, 200);

        ResizeFiles(source);
        var output = WaitForResizedCopies(new[] { source }, expectedCount: 1).Single();
        Assert.AreEqual(
            "format - Format - 100 - 100 - 100 - 50.png",
            Path.GetFileName(output),
            "The resized image filename did not apply all six format parameters.");
    }

    [TestMethod("ImageResizer.Resize.KeepDateModified")]
    [TestCategory("Image Resizer")]
    public void KeepsOriginalModifiedDateWhenReplacingImage()
    {
        ConfigureResizeSettings(DefaultPreset, replace: true, keepDateModified: true);
        var source = CreateImageFixture("keep-date.png", 400, 200);
        var originalModifiedTime = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(source, originalModifiedTime);

        var dialog = OpenResizeDialog(source);
        AssertDialogCheckBox(dialog, "Overwrite files", expected: true);
        ClickResizeAndWait(dialog);

        AssertImageDimensions(source, 100, 50);
        Assert.AreEqual(
            originalModifiedTime,
            File.GetLastWriteTimeUtc(source),
            "Replacing the image changed its original modified timestamp.");
        Assert.AreEqual(0, GetResizedCopies(new[] { source }).Count, "Replacing the image created an unexpected copy.");
    }

    [TestMethod("ImageResizer.Resize.ShrinkOnly")]
    [TestCategory("Image Resizer")]
    public void ShrinkOnlyDoesNotEnlargeSmallerImage()
    {
        var largePreset = new ResizePreset("Large Target", ResizeFitMode.Fit, 800, 800, ResizeUnitMode.Pixel);
        ConfigureResizeSettings(largePreset, shrinkOnly: true);
        var source = CreateImageFixture("smaller.png", 400, 200);

        var dialog = OpenResizeDialog(source);
        AssertDialogCheckBox(dialog, "Make pictures smaller but not larger", expected: true);
        ClickResizeAndWait(dialog);

        var output = WaitForResizedCopies(new[] { source }, expectedCount: 1).Single();
        AssertImageDimensions(output, 400, 200);
    }

    [TestMethod("ImageResizer.Resize.ReplaceOriginal")]
    [TestCategory("Image Resizer")]
    public void ReplacesOriginalImageWithoutCreatingCopy()
    {
        ConfigureResizeSettings(DefaultPreset, replace: true);
        var source = CreateImageFixture("replace.png", 400, 200);

        var dialog = OpenResizeDialog(source);
        AssertDialogCheckBox(dialog, "Overwrite files", expected: true);
        ClickResizeAndWait(dialog);

        AssertImageDimensions(source, 100, 50);
        Assert.AreEqual(0, GetResizedCopies(new[] { source }).Count, "Replacing the image created an unexpected copy.");
    }

    [TestMethod("ImageResizer.Resize.Orientation")]
    [TestCategory("Image Resizer")]
    public void UncheckedIgnoreOrientationUsesUnswappedDimensions()
    {
        var portraitTarget = new ResizePreset("Portrait", ResizeFitMode.Stretch, 100, 200, ResizeUnitMode.Pixel);

        ConfigureResizeSettings(portraitTarget, ignoreOrientation: true);
        var swappedSource = CreateImageFixture("orientation-ignored.png", 400, 200);
        ResizeFiles(swappedSource);
        var swappedOutput = WaitForResizedCopies(new[] { swappedSource }, expectedCount: 1).Single();
        AssertImageDimensions(swappedOutput, 200, 100);

        ConfigureResizeSettings(portraitTarget, ignoreOrientation: false);
        var unswappedSource = CreateImageFixture("orientation-honored.png", 400, 200);
        var dialog = OpenResizeDialog(unswappedSource);
        AssertDialogCheckBox(dialog, "Ignore the orientation of pictures", expected: false);
        ClickResizeAndWait(dialog);

        var unswappedOutput = WaitForResizedCopies(new[] { unswappedSource }, expectedCount: 1).Single();
        AssertImageDimensions(unswappedOutput, 100, 200);
    }

    private static Session NavigateToImageResizerSettings()
    {
        var settings = Session.FromProcess(
            "PowerToys.Settings",
            PowerToysModule.PowerToysSettings,
            timeoutMS: 15_000);
        if (WaitForElementSearch(settings, By.AccessibilityId("AddSizeButton"), timeoutMS: 5_000))
        {
            return settings;
        }

        if (!WaitForElementSearch(settings, By.AccessibilityId("ImageResizerNavItem"), timeoutMS: 5_000))
        {
            settings.Find<NavigationViewItem>(By.AccessibilityId("FileManagementNavItem")).Click(msPostAction: 500);
            Assert.IsTrue(
                WaitForElementSearch(settings, By.AccessibilityId("ImageResizerNavItem"), timeoutMS: 5_000),
                "The File Management navigation group did not expose Image Resizer.");
        }

        settings.Find<NavigationViewItem>(By.AccessibilityId("ImageResizerNavItem")).Click(msPostAction: 500);
        Assert.IsTrue(
            WaitForElementSearch(settings, By.AccessibilityId("AddSizeButton"), timeoutMS: 60_000),
            "Image Resizer settings page did not become ready.");
        return settings;
    }

    private static bool WaitForElementSearch(Session session, By by, int timeoutMS) =>
        session.WaitFor(
            () => session.Has(by, timeoutMS: 500),
            timeoutMS: timeoutMS,
            pollIntervalMS: 200);

    private static ToggleSwitch SetModuleEnabled(ToggleSwitch toggle, bool enabled)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                toggle.Toggle(enabled);
                Assert.IsTrue(
                    toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", timeoutMS: 5_000),
                    $"Image Resizer enable switch did not settle to {(enabled ? "On" : "Off")}.");
                return toggle;
            }
            catch (TimeoutException) when (attempt < 2)
            {
                var settings = Session.FromProcess(
                    "PowerToys.Settings",
                    PowerToysModule.PowerToysSettings,
                    timeoutMS: 15_000);
                toggle = settings.Find<ToggleSwitch>(By.Name("Image Resizer"), timeoutMS: 15_000);
            }
        }

        return toggle;
    }

    private Session OpenResizeDialog(params string[] filePaths)
    {
        Assert.IsTrue(filePaths.Length > 0, "At least one image must be selected.");
        var folderPath = Path.GetDirectoryName(filePaths[0])!;
        Assert.IsTrue(
            filePaths.All(path => string.Equals(Path.GetDirectoryName(path), folderPath, StringComparison.OrdinalIgnoreCase)),
            "All selected images must be in the same folder.");

        var explorer = OpenExplorer(folderPath);
        var menuDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        Session? menu = null;
        Element? resizeMenuItem = null;
        var selectionFailures = 0;
        do
        {
            var selected = TrySelectFilesStable(explorer, filePaths, timeoutMS: 12_000);
            if (selected is null)
            {
                // A view opened during the one-time shell restart can stay empty; reopen it.
                if (++selectionFailures >= 2)
                {
                    selectionFailures = 0;
                    explorer = OpenExplorer(folderPath);
                }

                Thread.Sleep(300);
                continue;
            }

            selectionFailures = 0;
            explorer = selected;
            menu = OpenContextMenu(explorer);
            if (menu is not null)
            {
                resizeMenuItem = FindVisibleMenuItem(menu, ContextMenuCaption, timeoutMS: 5_000);
                if (resizeMenuItem is not null)
                {
                    break;
                }
            }

            KeyboardHelper.SendKeys(Key.Esc);
            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < menuDeadline);

        Assert.IsNotNull(menu, "Explorer did not open the expected image-file context-menu surface.");
        Assert.IsNotNull(
            resizeMenuItem,
            $"Explorer did not show the '{ContextMenuCaption}' command for the selected image(s).");
        resizeMenuItem!.Invoke(msPostAction: 300);

        var dialog = WindowsFinder.WaitForWindowByApp(
            ImageResizerProcessName,
            window => window.Width >= 400 && window.Height > 0,
            timeoutMS: DialogTimeoutMS);
        Assert.IsNotNull(dialog, "The Image Resizer window did not open after invoking its context-menu command.");
        Assert.IsTrue(
            dialog!.WaitForElement(By.AccessibilityId("SizeComboBox"), timeoutMS: 10_000),
            "The Image Resizer input page did not become ready.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(dialog.WindowHandle), timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            $"The Image Resizer window did not become foreground. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return dialog;
    }

    private void ResizeFiles(params string[] filePaths)
    {
        var dialog = OpenResizeDialog(filePaths);
        ClickResizeAndWait(dialog);
    }

    private static void ClickResizeAndWait(Session dialog)
    {
        var resizeButton = FindExact<Button>(dialog, "Resize");
        Assert.IsNotNull(resizeButton, "The Image Resizer input page did not expose its Resize button.");
        resizeButton!.Click();
        Assert.IsTrue(
            WaitForProcess(ImageResizerProcessName, expected: false, timeoutMS: ResizeTimeoutMS),
            $"{ImageResizerProcessName} did not exit after completing the resize operation.");
    }

    private static void AssertDialogCheckBox(Session dialog, string name, bool expected)
    {
        var checkBox = FindExact<CheckBox>(dialog, name);
        Assert.IsNotNull(checkBox, $"The Image Resizer window did not expose the '{name}' checkbox.");
        Assert.AreEqual(expected, checkBox!.IsChecked, $"The '{name}' checkbox had the wrong state.");
    }

    private void AssertContextMenuPresence(
        Session explorer,
        string[] filePaths,
        bool expected)
    {
        var folder = Path.GetDirectoryName(filePaths[0])!;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        var lastObservation = new ContextMenuObservation(false, false);
        var selectionFailures = 0;

        do
        {
            KeyboardHelper.SendKeys(Key.Esc);

            // On a slow agent Explorer can render its file view asynchronously after the module
            // toggles (or open an empty view right after the one-time shell restart). Re-establish a
            // stable selection before each attempt and reopen a fresh window if it keeps failing.
            var selected = TrySelectFilesStable(explorer, filePaths, timeoutMS: 12_000);
            if (selected is null)
            {
                TestContext.WriteLine(
                    $"Selection not established. folder='{folder}' exists={Directory.Exists(folder)} " +
                    $"files=[{(Directory.Exists(folder) ? string.Join(", ", Directory.GetFiles(folder).Select(Path.GetFileName)) : "<none>")}] " +
                    $"fixtureOnDisk={filePaths.All(File.Exists)} temp='{Path.GetTempPath()}'.");
                if (++selectionFailures >= 2)
                {
                    selectionFailures = 0;
                    explorer = OpenExplorer(folder);
                }

                Thread.Sleep(300);
                continue;
            }

            selectionFailures = 0;
            explorer = selected;
            var menu = OpenContextMenu(explorer);
            if (menu is null)
            {
                Thread.Sleep(300);
                continue;
            }

            var stableObservation = WaitHelper.WaitForStable(
                observe: () => ObserveContextMenu(menu),
                isMatch: observation => observation is not null && observation.IsOpen && observation.CommandPresent == expected,
                timeoutMS: 8_000,
                requiredConsecutiveMatches: 4,
                pollIntervalMS: 250);
            lastObservation = stableObservation.LastObservation ?? lastObservation;
            KeyboardHelper.SendKeys(Key.Esc);

            if (stableObservation.Succeeded)
            {
                return;
            }

            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        var surface = UseModernContextMenu ? "modern" : "classic";
        Assert.IsTrue(
            lastObservation.IsOpen,
            $"The {surface} Explorer context menu did not become ready.");
        Assert.AreEqual(
            expected,
            lastObservation.CommandPresent,
            $"The {surface} Explorer context menu did {(expected ? "not show" : "show")} '{ContextMenuCaption}'.");
    }

    private static Session? OpenContextMenu(Session explorer)
    {
        EnsureExplorerForeground(explorer);
        KeyboardHelper.SendKeys(Key.Esc);

        // Windows 11 shows the modern (tier-1) surface directly; Windows 10 the classic one. The
        // Image Resizer command is registered into whichever the OS shows, so no "Show more options"
        // step (and no classic fallback on Windows 11) is needed.
        var surfaceClass = IsWindows11OrNewer() ? ModernContextMenuClassName : ClassicContextMenuClassName;
        if (!WindowControl.TryOpenContextMenuForFocusedControl(new IntPtr(explorer.WindowHandle)))
        {
            return null;
        }

        return WaitForContextMenuSurface(surfaceClass, timeoutMS: 15_000);
    }

    private static Session? WaitForContextMenuSurface(
        string className,
        int timeoutMS) =>
        WindowsFinder.WaitForWindow(
            window => IsContextMenuClass(window.ClassName, className),
            timeoutMS: timeoutMS,
            pollIntervalMS: 100);

    private static bool IsContextMenuClass(string actualClassName, string expectedClassName) =>
        expectedClassName == ClassicContextMenuClassName
            ? actualClassName.Equals(expectedClassName, StringComparison.OrdinalIgnoreCase)
            : actualClassName.Contains(expectedClassName, StringComparison.OrdinalIgnoreCase);

    private static ContextMenuObservation ObserveContextMenu(Session menu)
    {
        var menuReady = menu.WindowHandle != 0 &&
            WindowsFinder.ListAll().Any(window => window.Hwnd == menu.WindowHandle);
        if (!menuReady)
        {
            return new ContextMenuObservation(false, false);
        }

        try
        {
            return new ContextMenuObservation(true, HasVisibleMenuItem(menu, ContextMenuCaption));
        }
        catch (Exception)
        {
            // The transient menu popup can vanish mid-query (winappcli reports its HWND as gone);
            // treat it as not-yet-stable so the caller reopens it.
            return new ContextMenuObservation(false, false);
        }
    }

    private static bool HasVisibleMenuItem(Session menu, string name) =>
        FindVisibleMenuItem(menu, name, timeoutMS: 250) is not null;

    private static Element? FindVisibleMenuItem(Session menu, string name, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            var item = menu.FindAll<Element>(By.Name(name), timeoutMS: 250)
                .FirstOrDefault(element =>
                    element.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    element.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) &&
                    element.Displayed &&
                    element.Width > 0 &&
                    element.Height > 0);
            if (item is not null)
            {
                return item;
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private Session OpenExplorer(string folderPath)
    {
        EnsureContextMenuHandlerLoaded();
        CloseExplorerFileWindows();
        var existingHandles = WindowsFinder.ListByApp(ExplorerProcessName)
            .Where(IsExplorerFileWindow)
            .Select(window => window.Hwnd)
            .ToHashSet();

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/n,\"{folderPath}\"",
            UseShellExecute = true,
        });

        var explorer = WindowsFinder.WaitForWindowByApp(
            ExplorerProcessName,
            window => IsExplorerFileWindow(window) && !existingHandles.Contains(window.Hwnd),
            timeoutMS: ExplorerTimeoutMS);
        Assert.IsNotNull(explorer, $"Explorer did not open '{folderPath}'.");

        explorerWindowHandle = explorer!.WindowHandle;
        EnsureExplorerForeground(explorer);
        return explorer;
    }

    // Both context-menu handlers are registered at runtime when the module is enabled (the classic
    // registry-COM handler always, plus the modern sparse-MSIX package on signed builds). An
    // Explorer that was already running only surfaces them after the shell restarts, so do it once.
    private static void EnsureContextMenuHandlerLoaded()
    {
        if (contextMenuExplorerRefreshed)
        {
            return;
        }

        contextMenuExplorerRefreshed = true;
        Thread.Sleep(3_000);

        var previousProcessIds = Process.GetProcessesByName(ExplorerProcessName)
            .Select(process =>
            {
                var id = process.Id;
                process.Dispose();
                return id;
            })
            .ToHashSet();

        WindowControl.TryKillProcessByName(ExplorerProcessName);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var current = Process.GetProcessesByName(ExplorerProcessName);
            var hasFreshShell = current.Any(process => !previousProcessIds.Contains(process.Id));
            foreach (var process in current)
            {
                process.Dispose();
            }

            if (hasFreshShell)
            {
                break;
            }

            Thread.Sleep(500);
        }

        Thread.Sleep(2_000);
    }

    private static Session SelectFiles(Session explorer, params string[] filePaths)
    {
        var selection = ExplorerShell.SetSelectionAndWaitForStable(
            new IntPtr(explorer.WindowHandle),
            filePaths,
            filePaths[0],
            timeoutMS: ExplorerTimeoutMS,
            requiredConsecutiveMatches: 4);
        if (!selection.Succeeded)
        {
            var replacement = FindReplacementExplorer(explorer, Path.GetDirectoryName(filePaths[0])!);
            if (replacement is not null)
            {
                explorer = replacement;
                selection = ExplorerShell.SetSelectionAndWaitForStable(
                    new IntPtr(explorer.WindowHandle),
                    filePaths,
                    filePaths[0],
                    timeoutMS: ExplorerTimeoutMS,
                    requiredConsecutiveMatches: 4);
            }
        }

        var observedSelection = selection.LastObservation;
        var selectedPaths = observedSelection is null ? "<none>" : string.Join(", ", observedSelection.SelectedPaths);
        var normalizedPaths = filePaths
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exactTerminalSelection = observedSelection is not null &&
            observedSelection.SelectedPaths.SetEquals(normalizedPaths) &&
            string.Equals(
                observedSelection.FocusedPath,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(filePaths[0])),
                StringComparison.OrdinalIgnoreCase) &&
            WindowControl.GetForegroundWindowHandle() == new IntPtr(explorer.WindowHandle);

        Assert.IsTrue(
            selection.Succeeded || exactTerminalSelection,
            $"Explorer selection did not settle. Last selected paths: [{selectedPaths}]. " +
            $"Last focused path: '{observedSelection?.FocusedPath ?? "<none>"}'. " +
            $"Expected Explorer HWND: {explorer.WindowHandle}. " +
            $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return explorer;
    }

    // Non-throwing selection used by the context-menu retry loop: re-establishes a stable selection
    // (handling an Explorer window that was replaced mid-render) and returns the live session, or
    // null if it could not settle within the timeout so the caller can retry.
    private static Session? TrySelectFilesStable(Session explorer, string[] filePaths, int timeoutMS)
    {
        if (ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(explorer.WindowHandle), filePaths, filePaths[0], timeoutMS, requiredConsecutiveMatches: 4).Succeeded)
        {
            return explorer;
        }

        var replacement = FindReplacementExplorer(explorer, Path.GetDirectoryName(filePaths[0])!);
        if (replacement is not null &&
            ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(replacement.WindowHandle), filePaths, filePaths[0], timeoutMS, requiredConsecutiveMatches: 4).Succeeded)
        {
            return replacement;
        }

        return null;
    }

    private static Session? FindReplacementExplorer(Session explorer, string folderPath)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
        var foregroundWindow = WindowControl.GetForegroundWindowHandle().ToInt64();
        var replacement = WindowsFinder.ListByApp(ExplorerProcessName)
            .Where(IsExplorerFileWindow)
            .Where(window => window.Hwnd != explorer.WindowHandle)
            .Where(window => window.Title.Contains(folderName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(window => window.Hwnd == foregroundWindow)
            .FirstOrDefault();
        if (replacement is null)
        {
            return null;
        }

        return WindowsFinder.WaitForWindow(
            window => window.Hwnd == replacement.Hwnd,
            timeoutMS: 2_000,
            pollIntervalMS: 100);
    }

    private static void EnsureExplorerForeground(Session explorer)
    {
        Assert.IsTrue(
            WindowControl.WaitForForeground(
                new IntPtr(explorer.WindowHandle),
                ExplorerTimeoutMS,
                requiredConsecutiveMatches: 3),
            $"Explorer HWND {explorer.WindowHandle} was not the stable foreground window. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
    }

    private string CreateTestFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PowerToys-ImageResizer-UITests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        temporaryFolders.Add(folder);
        return folder;
    }

    private string CreateImageFixture(string fileName, int width, int height)
    {
        return CreateImageFixture(CreateTestFolder(), fileName, width, height);
    }

    private static string CreateImageFixture(string folder, string fileName, int width, int height)
    {
        var path = Path.Combine(folder, fileName);
        using var image = new Bitmap(width, height);
        image.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.CornflowerBlue);
        }

        var imageFormat = Path.GetExtension(fileName).Equals(".gif", StringComparison.OrdinalIgnoreCase)
            ? System.Drawing.Imaging.ImageFormat.Gif
            : System.Drawing.Imaging.ImageFormat.Png;
        image.Save(path, imageFormat);

        // Confirm the fixture actually reached disk before Explorer is asked to show it; retry once
        // to absorb a slow or locked temp on constrained agents.
        if (!File.Exists(path))
        {
            Thread.Sleep(500);
            image.Save(path, imageFormat);
        }

        Assert.IsTrue(File.Exists(path), $"Image fixture was not written to disk at '{path}' (temp='{Path.GetTempPath()}').");
        return path;
    }

    private static string CreateStripedImageFixture(string folder, string fileName, int width, int height)
    {
        var path = Path.Combine(folder, fileName);
        using var image = new Bitmap(width, height);
        image.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.Green);
            using var red = new SolidBrush(Color.Red);
            using var blue = new SolidBrush(Color.Blue);
            graphics.FillRectangle(red, 0, 0, width / 4, height);
            graphics.FillRectangle(blue, width * 3 / 4, 0, width - (width * 3 / 4), height);
        }

        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    private static void AssertImageDimensions(string path, int expectedWidth, int expectedHeight)
    {
        using var image = Image.FromFile(path);
        Assert.AreEqual(expectedWidth, image.Width, $"Unexpected width for '{path}'.");
        Assert.AreEqual(expectedHeight, image.Height, $"Unexpected height for '{path}'.");
    }

    private static void AssertPixelDominatedBy(string path, int x, int y, ColorChannel expectedChannel)
    {
        using var image = new Bitmap(path);
        var pixel = image.GetPixel(x, y);
        var expected = expectedChannel switch
        {
            ColorChannel.Red => pixel.R,
            ColorChannel.Green => pixel.G,
            ColorChannel.Blue => pixel.B,
            _ => 0,
        };
        var otherMaximum = expectedChannel switch
        {
            ColorChannel.Red => Math.Max(pixel.G, pixel.B),
            ColorChannel.Green => Math.Max(pixel.R, pixel.B),
            ColorChannel.Blue => Math.Max(pixel.R, pixel.G),
            _ => byte.MaxValue,
        };

        Assert.IsTrue(
            expected >= otherMaximum + 40,
            $"Pixel ({x}, {y}) in '{path}' was {pixel}, not predominantly {expectedChannel}.");
    }

    private static IReadOnlyList<string> WaitForResizedCopies(IReadOnlyCollection<string> sourcePaths, int expectedCount)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        IReadOnlyList<string> copies;
        do
        {
            copies = GetResizedCopies(sourcePaths);
            if (copies.Count == expectedCount)
            {
                return copies;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        Assert.AreEqual(expectedCount, copies.Count, "The expected resized image copies were not created.");
        return copies;
    }

    private static IReadOnlyList<string> GetResizedCopies(IReadOnlyCollection<string> sourcePaths)
    {
        var sources = sourcePaths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folder = Path.GetDirectoryName(sourcePaths.First())!;
        return Directory.EnumerateFiles(folder)
            .Select(Path.GetFullPath)
            .Where(path => !sources.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool WaitForProcess(string processName, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var processes = Process.GetProcessesByName(processName);
            var running = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            if (running == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static T? FindExact<T>(Session session, string name, int timeoutMS = 5_000)
        where T : Element, new()
    {
        return session.FindAll<T>(By.Name(name), timeoutMS)
            .FirstOrDefault(element => element.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void ConfigureResizeSettings(
        ResizePreset preset,
        bool shrinkOnly = false,
        bool replace = false,
        bool ignoreOrientation = true,
        bool keepDateModified = false,
        string fileNameFormat = "%1 (%2)")
    {
        ConfigureResizeSettings(
            new[] { preset },
            shrinkOnly,
            replace,
            ignoreOrientation,
            keepDateModified,
            fileNameFormat);
    }

    private static void ConfigureResizeSettings(
        IReadOnlyList<ResizePreset> presets,
        bool shrinkOnly = false,
        bool replace = false,
        bool ignoreOrientation = true,
        bool keepDateModified = false,
        string fileNameFormat = "%1 (%2)")
    {
        var sizes = new JsonArray();
        for (var index = 0; index < presets.Count; index++)
        {
            sizes.Add(CreatePresetNode(presets[index], index));
        }

        var properties = new JsonObject
        {
            ["imageresizer_selectedSizeIndex"] = WrappedValue(0),
            ["imageresizer_shrinkOnly"] = WrappedValue(shrinkOnly),
            ["imageresizer_replace"] = WrappedValue(replace),
            ["imageresizer_ignoreOrientation"] = WrappedValue(ignoreOrientation),
            ["imageresizer_removeMetadata"] = WrappedValue(false),
            ["imageresizer_jpegQualityLevel"] = WrappedValue(90),
            ["imageresizer_pngInterlaceOption"] = WrappedValue(0),
            ["imageresizer_tiffCompressOption"] = WrappedValue(0),
            ["imageresizer_fileName"] = WrappedValue(fileNameFormat),
            ["imageresizer_sizes"] = new JsonObject { ["value"] = sizes },
            ["imageresizer_keepDateModified"] = WrappedValue(keepDateModified),
            ["imageresizer_fallbackEncoder"] = WrappedValue("19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
            ["imageresizer_customSize"] = new JsonObject
            {
                ["value"] = CreatePresetNode(
                    new ResizePreset("custom", ResizeFitMode.Fit, 1024, 640, ResizeUnitMode.Pixel),
                    presets.Count),
            },
        };
        var settings = new JsonObject
        {
            ["name"] = ImageResizerModuleName,
            ["version"] = "1",
            ["properties"] = properties,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(ImageResizerSettingsPath)!);
        File.WriteAllText(ImageResizerSettingsPath, settings.ToJsonString(IndentedJson));
    }

    private static JsonObject CreatePresetNode(ResizePreset preset, int id) => new()
    {
        ["Id"] = id,
        ["name"] = preset.Name,
        ["fit"] = (int)preset.Fit,
        ["width"] = preset.Width,
        ["height"] = preset.Height,
        ["unit"] = (int)preset.Unit,
    };

    private static JsonObject WrappedValue<T>(T value) => new()
    {
        ["value"] = JsonValue.Create(value),
    };

    private static void TryRestoreSettingsFile(string path, bool existed, string? content)
    {
        try
        {
            if (existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content!);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not restore Image Resizer settings file '{path}'. {ex.Message}");
        }
    }

    private static bool IsWindows11OrNewer() => Environment.OSVersion.Version.Build >= 22_000;

    private static bool WaitForModernPackageRegistration(int timeoutMS)
    {
        if (!IsWindows11OrNewer())
        {
            return true;
        }

        return WaitHelper.WaitForStable(
            observe: ModernPackageRegistered,
            isMatch: registered => registered,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250).Succeeded;
    }

    private static bool ModernPackageRegistered()
    {
        try
        {
            return new Windows.Management.Deployment.PackageManager()
                .FindPackagesForUser(string.Empty)
                .Any(package => package.Id.Name.Contains(ModernPackageName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    // Windows 11 shows the tier-1 (modern, sparse-MSIX) context menu; Windows 10 shows the classic
    // (registry-COM) menu. CI signs the sparse package so the modern menu registers, so the test
    // drives the real per-OS surface with no classic fallback on Windows 11.
    private static bool UseModernContextMenu => IsWindows11OrNewer();

    private static bool IsExplorerFileWindow(WindowsFinder.WindowInfo window) =>
        window.ClassName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase);

    private static bool CloseExplorerFileWindows() =>
        WindowControl.TryCloseByApp(ExplorerProcessName, IsExplorerFileWindow, timeoutMS: 10_000);

    private static bool CloseImageResizerWindows()
    {
        if (WindowControl.TryCloseByApp(ImageResizerProcessName, timeoutMS: 5_000) &&
            WaitForProcess(ImageResizerProcessName, expected: false, timeoutMS: 1_000))
        {
            return true;
        }

        return WindowControl.TryKillProcessTreeByNameAndWait(ImageResizerProcessName, timeoutMS: 10_000);
    }

    private static bool DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (attempt < 4)
            {
                Thread.Sleep(250);
            }
        }

        return !Directory.Exists(path);
    }

    private sealed record ContextMenuObservation(bool IsOpen, bool CommandPresent);

    private sealed record ResizePreset(
        string Name,
        ResizeFitMode Fit,
        double Width,
        double Height,
        ResizeUnitMode Unit);

    private enum ResizeFitMode
    {
        Fill,
        Fit,
        Stretch,
    }

    private enum ResizeUnitMode
    {
        Centimeter,
        Inch,
        Percent,
        Pixel,
    }

    private enum ColorChannel
    {
        Red,
        Green,
        Blue,
    }
}
