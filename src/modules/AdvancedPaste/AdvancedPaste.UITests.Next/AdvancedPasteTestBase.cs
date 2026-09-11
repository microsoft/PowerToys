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
                TestFileCleanup.Run(
                    generatedFiles,
                    testDirectory,
                    TestContext.CurrentTestOutcome != UnitTestOutcome.Passed,
                    StopSharedScope,
                    message => TestContext.WriteLine(message));
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
        Target.Invoke(() => KeyboardHelper.SendChord(keys));
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
        Element? FindAction() => window.FindAll<Element>(By.Name(name), 0)
            .FirstOrDefault(element => element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) &&
                element.Name.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase));

        var located = WaitHelper.WaitForStable(
            FindAction,
            candidate => candidate is not null && candidate.IsEnabled,
            timeoutMS: 15_000,
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        var action = located.LastObservation;
        Assert.IsTrue(located.Succeeded, $"Action '{name}' did not become enabled. Last exception: {located.LastException}.");
        Assert.IsNotNull(action, $"Enabled action '{name}' was not exposed as an accessible list item. Last exception: {located.LastException}.");
        if (action.IsOffscreen)
        {
            action.ScrollIntoView();
        }

        action.Invoke(msPostAction: 0);
    }

    protected void SetClipboardText(string text)
    {
        Step("Setting and verifying the text clipboard fixture");
        AccessClipboard(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text);
            return true;
        });
        WaitUntil(
            () => ReadClipboardText() == text,
            "The source text did not become readable on the clipboard.");
    }

    protected string ReadClipboardText() => AccessClipboard(() =>
        System.Windows.Forms.Clipboard.GetText(System.Windows.Forms.TextDataFormat.UnicodeText));

    protected string[] ReadClipboardFormats() => AccessClipboard(() =>
        WinClipboard.GetContent().AvailableFormats.Order().ToArray());

    protected void SetClipboard(DataPackage package)
    {
        AccessClipboard(() =>
        {
            WinClipboard.SetContent(package);
            WinClipboard.Flush();
            return true;
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

    protected void SetFileClipboard(params string[] paths)
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
}
