// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

[TestClass]
public abstract class AdvancedPasteTestBase : UITestBase
{
    protected const string ProcessName = "PowerToys.AdvancedPaste";
    protected static readonly Key[] ActivationShortcut = [Key.LWin, Key.Shift, Key.V];
    protected static readonly string SettingsPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "AdvancedPaste", "settings.json");

    private readonly List<string> generatedFiles = [];
    private static IDisposable? settingsSnapshot;
    private DataPackage? clipboardSnapshot;
    private PasteTarget? target;
    private DirectoryInfo? testDirectory;
    private bool cleanedUp;

    protected AdvancedPasteTestBase()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["AdvancedPaste"])
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    protected override IReadOnlyList<string> StaleProcessNames => ["PowerToys", "PowerToys.Settings", ProcessName];

    private protected PasteTarget Target => target ?? throw new InvalidOperationException("The paste destination has not been created.");

    protected string TestDirectory => testDirectory?.FullName ?? throw new InvalidOperationException("The fixture directory has not been created.");

    protected override void PrepareTestState()
    {
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(ProcessName), "Advanced Paste did not stop before preparing settings.");
        settingsSnapshot ??= SettingsConfigHelper.PreserveModuleSettings("AdvancedPaste");
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var settings = JsonNode.Parse(File.ReadAllText(FixturePath("settings.json")))!.AsObject();
        settings["properties"]!["EnableClipboardPreview"] = new JsonObject { ["value"] = true };
        var additional = settings["properties"]!["additional-actions"]!;
        additional["image-to-text"]!["shortcut"] = DirectShortcut(Key.I);
        additional["paste-as-file"]!["paste-as-txt-file"]!["shortcut"] = DirectShortcut(Key.T);
        additional["paste-as-file"]!["paste-as-png-file"]!["shortcut"] = DirectShortcut(Key.P);
        additional["paste-as-file"]!["paste-as-html-file"]!["shortcut"] = DirectShortcut(Key.H);
        additional["transcode"]!["transcode-to-mp3"]!["shortcut"] = DirectShortcut(Key.F7);
        additional["transcode"]!["transcode-to-mp4"]!["shortcut"] = DirectShortcut(Key.F8);
        File.WriteAllText(SettingsPath, settings.ToJsonString());
    }

    [TestInitialize]
    public async Task PreparePasteTest()
    {
        try
        {
            Step("Waiting for the Settings navigation tree before preparing the paste destination");
            var settingsHandle = new IntPtr(Session.WindowHandle);
            var settingsReady = WaitHelper.WaitForStable(
                () => Session.Has(By.AccessibilityId("GeneralNavItem"), 0),
                visible => visible,
                timeoutMS: 30_000,
                requiredConsecutiveMatches: 2,
                shouldRetryException: exception =>
                    AdvancedPasteUi.IsStaleElement(exception) ||
                    (exception is AssertFailedException &&
                        exception.Message.Contains($"Window HWND {Session.WindowHandle} not found or not accessible.", StringComparison.Ordinal) &&
                        WindowControl.EnumerateProcessWindows([Session.ProcessId]).Any(window => window.Hwnd == settingsHandle)));
            Assert.IsTrue(
                settingsReady.Succeeded,
                $"Settings did not finish initializing its navigation tree. Last exception: {settingsReady.LastException}");
            WindowHelper.MaximizeWindow(settingsHandle);
            WaitUntil(() => WindowHelper.IsWindowMaximized(settingsHandle), "Settings did not retain its initialized maximized layout.");

            Step("Preparing the clipboard and a real rich-text paste destination");
            target = new PasteTarget();
            testDirectory = Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_UITests_");
            var original = WinClipboard.GetContent();
            var snapshot = new DataPackage();
            foreach (var format in original.AvailableFormats)
            {
                snapshot.SetData(format, await original.GetDataAsync(format).AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
            }

            clipboardSnapshot = snapshot;
            DismissAdvancedPaste();
            Assert.IsTrue(ClipboardHelper.Clear(), "Could not clear the clipboard before the test.");
            Target.Focus();
        }
        catch (Exception setupFailure)
        {
            await CaptureFailureArtifactsAsync();
            try
            {
                await CleanupPasteTest();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("The clipboard fixture could not initialize or clean up.", setupFailure, cleanupFailure);
            }

            throw;
        }
    }

    [TestCleanup]
    public async Task CleanupPasteTest()
    {
        if (cleanedUp)
        {
            return;
        }

        cleanedUp = true;
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            DismissAdvancedPaste();
            if (clipboardSnapshot is not null && target is not null)
            {
                SetClipboard(clipboardSnapshot);
            }
        }
        finally
        {
            try
            {
                target?.Dispose();
            }
            finally
            {
                foreach (var path in generatedFiles)
                {
                    File.Delete(path);
                    var parent = Path.GetDirectoryName(path)!;
                    if (Path.GetFileName(parent).StartsWith("PowerToys_AdvancedPaste_", StringComparison.Ordinal) &&
                        Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                    {
                        Directory.Delete(parent);
                    }
                }

                testDirectory?.Delete(recursive: true);
                if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
                {
                    // A failed conversion may still be running; do not let it overwrite the next fixture.
                    StopSharedScope();
                }
            }
        }
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void RestoreAdvancedPasteSettings()
    {
        StopSharedScope();
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(ProcessName), "Advanced Paste did not stop before restoring settings.");
        var snapshot = settingsSnapshot;
        settingsSnapshot = null;
        snapshot?.Dispose();
    }

    protected static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    protected void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    protected void SendShortcut(params Key[] keys)
    {
        Step($"Sending [{string.Join(", ", keys)}] on the fixture's input thread");
        Target.Invoke(() => TestKeyboard.SendChord(keys));
    }

    protected void NavigateToSettings()
    {
        Step("Navigating to Advanced Paste settings");
        if (!Session.Has(By.AccessibilityId("AdvancedPasteNavItem"), 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), 10_000).Invoke(msPostAction: 0);
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("AdvancedPasteNavItem"), 10_000).Invoke(msPostAction: 0);
        Session.Find(By.AccessibilityId("AdvancedPasteEnableToggleControlHeaderText"), 15_000);
    }

    protected void NavigateToGeneralSettings()
    {
        Step("Navigating to General settings and waiting for its loaded page");
        Session.Find<NavigationViewItem>(By.AccessibilityId("GeneralNavItem"), 10_000).Invoke(msPostAction: 0);
        WaitUntil(
            () => Session.Has(By.AccessibilityId("GeneralOpenUpdateSurfaceButton"), 0),
            "The General settings page did not finish loading.",
            timeoutMS: 30_000);
    }

    protected Session OpenAdvancedPaste(params Key[] shortcut)
    {
        Target.Focus();
        Step($"Destination before activation: {Target.State}");
        var window = OpenAdvancedPasteForWindow(Target.Handle, shortcut);
        var state = Target.State;
        Step($"Destination after activation: {state}");
        Assert.IsTrue(state.Visible && state.WindowState != System.Windows.Forms.FormWindowState.Minimized, "AP activation hid or minimized the paste destination.");
        return window;
    }

    protected Session OpenAdvancedPasteForWindow(IntPtr destination, params Key[] shortcut)
    {
        Step("Opening Advanced Paste through the Runner hotkey");
        Assert.AreEqual(destination, WindowControl.GetForegroundWindowHandle(), $"The paste destination is not foreground: {WindowControl.GetForegroundWindowInfo()}.");
        Assert.IsFalse(IsAdvancedPasteVisible(), "Advanced Paste was already visible before its activation shortcut.");
        SendShortcut(shortcut.Length == 0 ? ActivationShortcut : shortcut);
        WaitUntil(IsAdvancedPasteVisible, $"Advanced Paste did not open. Foreground: {WindowControl.GetForegroundWindowInfo()}.", timeoutMS: 30_000);
        var window = WindowsFinder.WaitForWindowByApp(ProcessName, w => w.Width > 100 && w.Height > 100, timeoutMS: 15_000);
        Assert.IsNotNull(window, "The Advanced Paste window was not discoverable.");
        window.Find(By.AccessibilityId("PasteOptionsListView"), 15_000);
        return window;
    }

    protected void DismissAdvancedPaste()
    {
        if (IsAdvancedPasteVisible())
        {
            Step("Dismissing Advanced Paste while preserving its process");
            WindowControl.TryCloseByApp(ProcessName);
            WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste remained visible after closing.");
        }
    }

    protected static bool IsAdvancedPasteVisible() =>
        WindowControl.EnumerateProcessWindows(GetModuleProcessIds()).Any(window =>
            window.IsVisible && window.Width > 100 && window.Height > 100);

    protected static int[] GetModuleProcessIds()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            return processes.Select(process => process.Id).ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    protected void SelectAction(Session window, string name)
    {
        Step($"Invoking '{name}'");
        var handle = new IntPtr(window.WindowHandle);
        WaitUntil(
            () => WindowControl.GetForegroundWindowHandle() == handle,
            $"Advanced Paste did not acquire foreground before selecting '{name}'.",
            timeoutMS: 10_000);
        var windowBounds = WindowHelper.GetWindowBounds(handle);
        MouseHelper.MoveTo((windowBounds.Left + windowBounds.Right) / 2, windowBounds.Top + 16);

        Element? FindAction() => window.FindAll<Element>(By.Name(name), 0)
            .FirstOrDefault(element => element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) &&
                element.Name.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase));

        // A matching tooltip or label can appear before the enabled action's ListItem.
        var located = WaitHelper.WaitForStable(
            FindAction,
            candidate => candidate is not null,
            timeoutMS: 15_000,
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        var action = located.LastObservation;
        Assert.IsNotNull(action, $"Enabled action '{name}' was not exposed as an accessible list item. Last exception: {located.LastException}.");
        if (action.IsOffscreen)
        {
            action.ScrollIntoView();
        }

        // Let first-show layout settle and hover tooltips disappear before resolving the click point.
        (int X, int Y, int Width, int Height, (int Left, int Top, int Right, int Bottom) Window)? previous = null;
        (bool Unchanged, bool Offscreen, IntPtr Foreground, bool PointerAtTarget)? lastInputState = null;
        var samples = 0;
        var sampling = Stopwatch.StartNew();
        var ready = WaitHelper.WaitForStable(
            FindAction,
            candidate =>
            {
                samples++;
                if (candidate is null)
                {
                    previous = null;
                    lastInputState = null;
                    return false;
                }

                var bounds = (candidate.X, candidate.Y, candidate.Width, candidate.Height, WindowHelper.GetWindowBounds(handle));
                var unchanged = previous == bounds;
                previous = bounds;
                var offscreen = candidate.IsOffscreen;
                var foreground = WindowControl.GetForegroundWindowHandle();
                var point = new System.Drawing.Point(candidate.X + (candidate.Width / 2), candidate.Y + (candidate.Height / 2));
                if (unchanged && candidate.Width > 0 && candidate.Height > 0 && !offscreen && foreground == handle &&
                    System.Windows.Forms.Cursor.Position != point)
                {
                    MouseHelper.MoveTo(point.X, point.Y);
                }

                var state = (
                    Unchanged: unchanged,
                    Offscreen: offscreen,
                    Foreground: foreground,
                    PointerAtTarget: System.Windows.Forms.Cursor.Position == point);
                lastInputState = state;
                return state.Unchanged && candidate.Width > 0 && candidate.Height > 0 && !state.Offscreen &&
                    state.Foreground == handle && state.PointerAtTarget;
            },
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        Step($"Action readiness for '{name}': HWND={handle}; samples={samples}; stable={ready.ConsecutiveMatches}; elapsed={sampling.Elapsed}; state={lastInputState} (unchanged, offscreen, foreground, pointer at target).");
        if (ready.LastObservation is { } observed)
        {
            var atPoint = WindowFromPoint(new System.Drawing.Point(observed.X + (observed.Width / 2), observed.Y + (observed.Height / 2)));
            var root = GetAncestor(atPoint, 2);
            var rootOwner = GetAncestor(atPoint, 3);
            Step($"Native point: child={atPoint}, root={root}, rootOwner={rootOwner}; expected={handle}; destination={Target.State}");
            foreach (var native in WindowControl.EnumerateAllWindows().Where(native =>
                native.Hwnd == root || native.Hwnd == rootOwner || native.Hwnd == handle ||
                native.ProcessId == window.ProcessId))
            {
                Step($"Native point window: {native}");
            }
        }

        Assert.IsTrue(
            ready.Succeeded,
            $"The '{name}' row and window did not settle for real input. Last bounds: {previous}; state: {lastInputState}; foreground: {WindowControl.GetForegroundWindowInfo()}; last exception: {ready.LastException}.");
        action = ready.LastObservation!;
        Step($"Clicking settled '{name}' row at ({action.X},{action.Y}) {action.Width}x{action.Height}");
        action.MouseClick(msPostAction: 0);
    }

    protected void SetClipboardText(string text)
    {
        Step("Setting and verifying the text clipboard fixture");
        AccessClipboard(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text);
            return true;
        });
        Assert.AreEqual(text, ReadClipboardText(), "The source text was not placed on the clipboard.");
    }

    protected string ReadClipboardText() => AccessClipboard(() =>
        System.Windows.Forms.Clipboard.GetText(System.Windows.Forms.TextDataFormat.UnicodeText));

    protected void SetClipboard(DataPackage package)
    {
        Target.Invoke(() =>
        {
            WinClipboard.SetContent(package);
            WinClipboard.Flush();
        });
    }

    protected void SetRichTextClipboard(string text, string rtf)
    {
        Step("Copying rich text from the real editor");
        Target.CopyRichText(rtf);
        WaitUntil(
            () => ReadClipboardText() == text &&
                AccessClipboard(() => System.Windows.Forms.Clipboard.ContainsData(System.Windows.Forms.DataFormats.Rtf)),
            "Copying the rich-text source did not produce its expected text and RTF clipboard formats.");
        Target.Clear();
    }

    protected void SetHtmlClipboard(string html, string? text = null)
    {
        Step("Setting the HTML clipboard fixture");
        var data = new System.Windows.Forms.DataObject();
        data.SetData(System.Windows.Forms.DataFormats.Html, autoConvert: false, HtmlFormatHelper.CreateHtmlFormat(html));
        if (text is not null)
        {
            data.SetData(System.Windows.Forms.DataFormats.UnicodeText, autoConvert: false, text);
        }

        Target.Invoke(() => System.Windows.Forms.Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 100));
        WaitUntil(
            () => WinClipboard.GetContent().Contains(StandardDataFormats.Html),
            "The HTML clipboard fixture was not available.");
        if (text is null)
        {
            Assert.IsFalse(WinClipboard.GetContent().Contains(StandardDataFormats.Text), "The HTML-only fixture unexpectedly supplied plain text.");
        }
    }

    protected Task SetFileClipboard(params string[] paths)
    {
        Step("Setting and verifying the file clipboard fixture");
        foreach (var path in paths)
        {
            Assert.IsTrue(File.Exists(path), $"Clipboard source file '{path}' does not exist.");
        }

        var files = new StringCollection();
        files.AddRange(paths);
        AccessClipboard(() =>
        {
            System.Windows.Forms.Clipboard.SetFileDropList(files);
            return true;
        });
        CollectionAssert.AreEqual(paths, ReadClipboardFilePaths(), "The file clipboard fixture was not established.");
        return Task.CompletedTask;
    }

    protected string[] ReadClipboardFilePaths() => AccessClipboard(() =>
        System.Windows.Forms.Clipboard.ContainsFileDropList()
            ? System.Windows.Forms.Clipboard.GetFileDropList().Cast<string>().ToArray()
            : []);

    private T AccessClipboard<T>(Func<T> action)
    {
        var result = WaitHelper.WaitForStable(
            () => Target.Invoke(action),
            _ => true,
            timeoutMS: 10_000,
            shouldRetryException: exception => exception is ExternalException { HResult: unchecked((int)0x800401D0) });
        Assert.IsTrue(result.Succeeded, $"The clipboard remained locked: {result.LastException?.Message}");
        return result.LastObservation!;
    }

    protected async Task SetBitmapClipboard(string path)
    {
        Step("Setting the bitmap clipboard fixture");
        var package = new DataPackage();
        package.SetBitmap(RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(path)));
        SetClipboard(package);
        WaitUntil(
            () => WinClipboard.GetContent().Contains(StandardDataFormats.Bitmap),
            "The bitmap clipboard fixture was not available.");
    }

    protected void TrackGeneratedFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Assert.IsTrue(
            fullPath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase),
            $"Advanced Paste returned a file outside the temporary directory: '{fullPath}'.");
        generatedFiles.Add(fullPath);
    }

    protected static JsonObject ReadProperties() =>
        JsonNode.Parse(File.ReadAllText(SettingsPath))!["properties"]!.AsObject();

    private static JsonObject DirectShortcut(Key key) => new()
    {
        ["win"] = true,
        ["ctrl"] = true,
        ["alt"] = true,
        ["shift"] = false,
        ["code"] = (int)key,
        ["key"] = string.Empty,
    };

    protected static void WaitForSetting(Func<JsonObject, bool> matches, string description) =>
        WaitUntil(
            () => matches(ReadProperties()),
            description,
            shouldRetryException: exception => exception is IOException or JsonException);

    protected static void WaitUntil(
        Func<bool> condition,
        string message,
        int timeoutMS = 15_000,
        Func<Exception, bool>? shouldRetryException = null)
    {
        var result = WaitHelper.WaitForStable(
            condition,
            value => value,
            timeoutMS,
            requiredConsecutiveMatches: 2,
            shouldRetryException: shouldRetryException ?? AdvancedPasteUi.IsStaleElement);
        Assert.IsTrue(result.Succeeded, $"{message} Last exception: {result.LastException?.Message}");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(System.Drawing.Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);
}
