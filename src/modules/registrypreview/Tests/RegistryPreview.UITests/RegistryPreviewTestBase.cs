// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.RegistryPreview.UITests;

/// <summary>
/// Shared fixture for the Registry Preview suites: a deterministic module baseline, temporary
/// <c>.reg</c> fixtures, and the launch/drive/assert helpers every test needs.
/// </summary>
/// <remarks>
/// Registry Preview has no runner-owned overlay/hotkey: the shell's "Preview" verb (registered
/// directly under <c>HKCU\Software\Classes\regfile\shell\preview</c> when the module is enabled;
/// see <c>getRegistryPreviewChangeSet</c> in <c>src/common/utils/modulesRegistry.h</c>) launches
/// <c>PowerToys.RegistryPreview.exe "&lt;path&gt;"</c> directly - there is no COM shell extension
/// and no sparse MSIX package to wait for. Settings owns module enablement, so the scope is still
/// the runner/Settings scope; the editor window itself is launched per test the same way the
/// shell verb does (the file path on the command line), mirroring the real activation path.
/// </remarks>
public abstract class RegistryPreviewTestBase : UITestBase
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    protected const string RegistryPreviewProcessName = "PowerToys.RegistryPreview";
    protected const string ModuleSettingsKey = "RegistryPreview";
    protected const int WindowTimeoutMS = 30_000;
    protected const int ActionTimeoutMS = 15_000;
    protected const int EditorTimeoutMS = 180_000;
    protected const int InteractiveRegistryImportTimeoutMS = 90_000;
    protected const string ContextMenuCaption = "Preview";

    private static readonly string[] ContextMenuWindowClasses = { "#32768", "Microsoft.UI.Content.PopupWindowSiteBridge" };
    private static readonly string[] ClassicContextMenuWindowClasses = { "#32768" };

    private readonly List<string> temporaryFolders = new();
    private readonly List<string> registrySubKeys = new();
    private readonly IDisposable moduleSettingsSnapshot = SettingsConfigHelper.PreserveModuleSettings(ModuleSettingsKey);

    protected RegistryPreviewTestBase()
        : base(PowerToysModule.PowerToysSettings)
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    protected override IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        RegistryPreviewProcessName,
    };

    [TestCleanup]
    public async Task CleanupRegistryPreviewTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        try
        {
            KeyboardHelper.SendKeys(Key.Esc);
        }
        catch (Win32Exception ex)
        {
            TestContext.WriteLine($"Cleanup could not send Escape, likely because UAC owns the secure desktop: {ex.Message}");
        }

        CloseRegistryPreviewWindows();
        WindowControl.TryCloseByApp("notepad", timeoutMS: 2_000);
        WindowControl.TryCloseByApp("regedit", timeoutMS: 2_000);
        CloseExplorerFileWindows();

        foreach (var subKey in registrySubKeys)
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }

        foreach (var folder in temporaryFolders)
        {
            if (!TryDeleteDirectory(folder))
            {
                TestContext.WriteLine($"Cleanup could not delete temporary folder '{folder}'.");
            }
        }

        registrySubKeys.Clear();
        temporaryFolders.Clear();
        moduleSettingsSnapshot.Dispose();
    }

    /// <summary>Timestamped trace so a CI hang names the step it stuck on.</summary>
    protected void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    // ---- fixtures -----------------------------------------------------------------------------
    protected string CreateTestFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PowerToys-RegistryPreview-UITests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        temporaryFolders.Add(folder);
        return folder;
    }

    /// <summary>
    /// Write a minimal, valid <c>.reg</c> fixture with one key that carries a string and a binary
    /// value - matching the exact repro shape from the sign-off checklist (issue #40675): "add new
    /// registry key with 1 string value and 1 binary value".
    /// </summary>
    protected string CreateRegFixture(string folder, string fileName, string keyPath, string stringValueData = "sample-value")
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, CreateRegContent(keyPath, stringValueData), Encoding.Unicode);
        Assert.IsTrue(File.Exists(path), $"Fixture .reg file was not written to disk at '{path}'.");
        return path;
    }

    protected static string CreateRegContent(string keyPath, string stringValueData = "sample-value") =>
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            $"[{keyPath}]\r\n" +
            $"\"SampleString\"=\"{stringValueData}\"\r\n" +
            "\"SampleBinary\"=hex:01,02,03,04\r\n";

    protected string CreateIsolatedRegistryKeyPath()
    {
        var subKey = $@"Software\PowerToysUITests\RegistryPreview\{Guid.NewGuid():N}";
        registrySubKeys.Add(subKey);
        return $@"HKEY_CURRENT_USER\{subKey}";
    }

    protected static string RegistrySubKeyFromFullPath(string keyPath)
    {
        const string prefix = @"HKEY_CURRENT_USER\";
        Assert.IsTrue(keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), $"Expected an HKCU path, got '{keyPath}'.");
        return keyPath[prefix.Length..];
    }

    private static bool TryDeleteDirectory(string path)
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
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }

        return false;
    }

    // ---- Registry Preview window lifecycle --------------------------------------------------

    /// <summary>
    /// Launch Registry Preview over <paramref name="filePath"/> (or with no argument when null),
    /// mirroring exactly how the shell's "Preview" verb and the Settings "Launch" action start
    /// the app: <c>PowerToys.RegistryPreview.exe "&lt;path&gt;"</c>.
    /// </summary>
    protected Session LaunchRegistryPreview(string? filePath = null)
    {
        CloseRegistryPreviewWindows();

        var exePath = SessionHelper.GetExecutablePath(PowerToysModule.RegistryPreview);
        Assert.IsTrue(File.Exists(exePath), $"Registry Preview executable not found at '{exePath}'.");

        Step($"Launching Registry Preview{(filePath is null ? " with no file" : $" with '{filePath}'")}");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = true,
        };
        if (filePath is not null)
        {
            startInfo.ArgumentList.Add(filePath);
        }

        using (Process.Start(startInfo) ?? throw new InvalidOperationException($"Process.Start returned null for '{exePath}'."))
        {
        }

        var window = WindowsFinder.WaitForWindowByApp(
            RegistryPreviewProcessName,
            info => info.Width > 0 && info.Height > 0,
            timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(window, "The Registry Preview window did not appear after launching it.");

        var session = Session.FromProcess(RegistryPreviewProcessName, PowerToysModule.RegistryPreview, timeoutMS: WindowTimeoutMS);
        Assert.IsTrue(
            session.WaitFor(() => session.Has(By.AccessibilityId("commandBar"), timeoutMS: 1_000), WindowTimeoutMS, pollIntervalMS: 500),
            "The Registry Preview window did not expose its command bar.");

        TryBringRegistryPreviewForward();
        return session;
    }

    protected Session LaunchRegistryPreviewWithEditor(string filePath)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            CloseRegistryPreviewWindows();
            ResetMonacoUserData();
            var session = LaunchRegistryPreview(filePath);
            if (WaitForEditorReadyStable(session))
            {
                return session;
            }

            if (attempt < 2)
            {
                CaptureMonacoStall(attempt);
                Step($"Monaco did not become ready after {EditorTimeoutMS}ms; restarting the Registry Preview process tree once.");
                var descendants = GetDescendantProcesses(session.ProcessId);
                var rootStopped = WindowControl.TryKillProcessTreeByNameAndWait(RegistryPreviewProcessName, timeoutMS: 10_000);
                StopCapturedProcesses(descendants);
                Assert.IsTrue(rootStopped, "Could not stop the stalled Registry Preview process tree before retrying.");
            }
        }

        Assert.Fail(
            $"Monaco did not become ready for '{Path.GetFileName(filePath)}' after a fresh Registry Preview process retry.");
        return null!;
    }

    private void ResetMonacoUserData()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "Microsoft",
            "PowerToys",
            "RegistryPreview-Temp");
        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 60)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (attempt < 60)
            {
                Thread.Sleep(500);
            }
        }

        Assert.Fail($"Could not reset Registry Preview's temporary WebView profile at '{path}'.");
    }

    private IReadOnlyCollection<Process> GetDescendantProcesses(int rootProcessId)
    {
        if (rootProcessId <= 0)
        {
            Step("Registry Preview PID was unavailable; no descendant processes were captured.");
            return Array.Empty<Process>();
        }

        var parents = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            Step($"CreateToolhelp32Snapshot failed with Win32 error {Marshal.GetLastWin32Error()}.");
            return Array.Empty<Process>();
        }

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var descendants = new HashSet<int>();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (processId, parentProcessId) in parents)
            {
                if (!descendants.Contains(processId) &&
                    (parentProcessId == rootProcessId || descendants.Contains(parentProcessId)))
                {
                    descendants.Add(processId);
                    changed = true;
                }
            }
        }

        var processes = new List<Process>();
        foreach (var processId in descendants)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                _ = process.Handle; // Pin the process identity so its PID cannot be reused during cleanup.
                processes.Add(process);
            }
            catch (Exception ex)
            {
                Step($"Could not pin captured descendant PID {processId}: {ex.Message}");
            }
        }

        Step($"Captured {processes.Count} Registry Preview descendant process(es): {string.Join(", ", processes.Select(process => $"{process.ProcessName}:{process.Id}"))}");
        return processes;
    }

    private void StopCapturedProcesses(IEnumerable<Process> processes)
    {
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5_000))
                    {
                        Step($"Captured descendant {process.ProcessName}:{process.Id} did not exit within 5 seconds.");
                    }
                }
            }
            catch (Exception ex)
            {
                Step($"Captured descendant PID {process.Id} could not be stopped: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private void CaptureMonacoStall(int attempt)
    {
        var path = Path.Combine(
            TestContext.TestResultsDirectory ?? Path.GetTempPath(),
            $"monaco-stall-attempt-{attempt}-{Guid.NewGuid():N}.png");
        if (ScreenCapture.TryCaptureDesktop(path))
        {
            TestContext.AddResultFile(path);
        }
    }

    /// <summary>
    /// Best-effort raise of the Registry Preview window. Interactions that need real input (typing
    /// into the Monaco editor, keyboard shortcuts for the common file dialogs) verify their own
    /// effect afterwards, so a foreground miss here is not fatal on its own.
    /// </summary>
    protected void TryBringRegistryPreviewForward()
    {
        var settled = WaitHelper.WaitForStable(
            observe: WindowControl.GetForegroundWindowInfo,
            isMatch: info => info.ProcessName.Contains("RegistryPreview", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 8_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250,
            recover: _ => WindowControl.TryFocusByApp(RegistryPreviewProcessName));

        if (!settled.Succeeded)
        {
            Step($"Registry Preview did not take the foreground; continuing. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }
    }

    protected static void CloseRegistryPreviewWindows()
    {
        if (WindowControl.TryCloseByApp(RegistryPreviewProcessName, timeoutMS: 5_000) &&
            WaitForProcess(RegistryPreviewProcessName, expected: false, timeoutMS: 2_000))
        {
            return;
        }

        WindowControl.TryKillProcessTreeByNameAndWait(RegistryPreviewProcessName, timeoutMS: 10_000);
    }

    protected static bool WaitForProcess(string processName, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (true)
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

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(250);
        }
    }

    // ---- driving the Monaco editor --------------------------------------------------------------

    /// <summary>
    /// Replace the whole editor content via the clipboard (select-all, paste). The Monaco editor is a
    /// real WebView2 document, not a UIA text control, so its displayed value can't be read back
    /// through UIA - but it accepts genuine keyboard input, and paste is the most reliable way to
    /// inject content that includes registry-file punctuation (<c>[ ] \ " =</c>) without depending on
    /// <see cref="System.Windows.Forms.SendKeys"/> escaping for every symbol.
    /// </summary>
    protected void ReplaceEditorContent(Session window, string newContent)
    {
        var editor = WaitForEditorReady(window);
        Step("Focusing the Monaco editor");
        editor.Click(msPostAction: 300);
        AssertRegistryPreviewOwnsForeground("before typing into the Monaco editor");

        Assert.IsTrue(ClipboardHelper.SetText(newContent), "Could not stage the new .reg content on the clipboard.");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.A);
        Thread.Sleep(150);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.V);
        Thread.Sleep(500);
    }

    protected static T? FindExact<T>(
        Session session,
        string name,
        int timeoutMS = 5_000,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        where T : Element, new() =>
        session.FindAll<T>(By.Name(name), timeoutMS)
            .FirstOrDefault(element => element.Name.Equals(name, comparison));

    protected static Element WaitForEditorReady(Session window) =>
        window.Find<Element>(By.AccessibilityId("Browser"), EditorTimeoutMS);

    private static bool WaitForEditorReadyStable(Session window)
    {
        var selector = By.AccessibilityId("Browser");
        if (!window.WaitForElement(selector, EditorTimeoutMS))
        {
            return false;
        }

        // IsLoading starts false during initial layout, so require the peer to remain visible after
        // Browser_Loaded has had time to switch into (or finish) the real WebView initialization.
        Thread.Sleep(1_000);
        return window.Has(selector, timeoutMS: 2_000);
    }

    protected static void AssertExactElement(Session session, string name, string description, int timeoutMS = ActionTimeoutMS)
    {
        Assert.IsNotNull(
            FindExact<Element>(session, name, timeoutMS),
            $"{description} '{name}' was not exposed through UI Automation.");
    }

    // ---- driving the classic Win32 Open/Save common dialogs --------------------------------------

    /// <summary>
    /// Registry Preview's Open/Save As buttons use the classic <c>comdlg32</c> <c>GetOpenFileName</c>/
    /// <c>GetSaveFileName</c> common dialogs directly (a workaround for the WinRT file pickers crashing
    /// while elevated - see <c>OpenFilePicker</c>/<c>SaveFilePicker</c> in RegistryPreviewUILib), not the
    /// modern WinRT picker. The dialog starts with focus already in the filename edit box, so typing the
    /// full path and pressing Enter is the standard, robust way to drive it without needing to resolve
    /// its internal control ids.
    /// </summary>
    protected void CompleteFileDialogWithPath(string path)
    {
        var dialog = WindowsFinder.WaitForWindow(
            info => info.ClassName.Equals("#32770", StringComparison.OrdinalIgnoreCase) &&
                    info.ProcessName.Contains("RegistryPreview", StringComparison.OrdinalIgnoreCase),
            timeoutMS: ActionTimeoutMS);
        Assert.IsNotNull(dialog, "The classic Open/Save common file dialog did not appear.");

        WindowControl.TryBringToForeground(new IntPtr(dialog!.WindowHandle));
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(dialog.WindowHandle), ActionTimeoutMS, requiredConsecutiveMatches: 2),
            $"The Registry Preview file dialog HWND {dialog.WindowHandle} did not become the stable foreground window.");

        Assert.IsTrue(ClipboardHelper.SetText(path), $"Could not place '{path}' on the clipboard.");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.A);
        Thread.Sleep(100);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.V);
        Thread.Sleep(200);
        KeyboardHelper.SendKey(Key.Enter);
    }

    // ---- Settings --------------------------------------------------------------------------------
    protected Session NavigateToRegistryPreviewSettings()
    {
        if (!Session.Has(By.AccessibilityId("RegistryPreviewNavItem"), timeoutMS: 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("AdvancedNavItem"), ActionTimeoutMS).Click(msPostAction: 500);
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("RegistryPreviewNavItem"), ActionTimeoutMS).Click(msPostAction: 800);
        Assert.IsTrue(
            Session.WaitForElement(By.AccessibilityId("RegistryPreviewLaunchButtonControl"), ActionTimeoutMS),
            "The Registry Preview settings page did not load.");
        return Session;
    }

    protected ToggleSwitch FindModuleToggle(Session settings)
    {
        var toggle = FindExact<ToggleSwitch>(settings, "Registry Preview", ActionTimeoutMS);
        Assert.IsNotNull(toggle, "The Registry Preview settings page did not expose its enable switch.");
        return toggle!;
    }

    protected ToggleSwitch SetModuleEnabled(Session settings, bool enabled)
    {
        var toggle = FindModuleToggle(settings);
        toggle.Toggle(enabled);
        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", timeoutMS: 5_000),
            $"Registry Preview enable switch did not settle to {(enabled ? "On" : "Off")}.");
        Assert.IsTrue(
            settings.WaitFor(() => IsPreviewVerbRegistered() == enabled, ActionTimeoutMS, pollIntervalMS: 250),
            $"The Registry Preview shell verb did not become {(enabled ? "registered" : "unregistered")}.");
        return FindModuleToggle(settings);
    }

    protected Session LaunchRegistryPreviewFromControl(Session owner, By selector, string origin)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Step($"Launching Registry Preview from {origin} (attempt {attempt})");
            owner.Find<Element>(selector, ActionTimeoutMS).Click(msPostAction: 300);

            var window = WindowsFinder.WaitForWindowByApp(
                RegistryPreviewProcessName,
                candidate => candidate.Width > 0 && candidate.Height > 0,
                timeoutMS: 20_000);
            if (window is null)
            {
                continue;
            }

            Assert.IsTrue(
                window.WaitForElement(By.AccessibilityId("commandBar"), EditorTimeoutMS),
                $"Registry Preview opened from {origin} but its command bar never became ready.");
            return window;
        }

        Assert.Fail($"Registry Preview did not open from {origin} after three attempts.");
        return null!;
    }

    protected static bool IsPreviewVerbRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\regfile\shell\preview\command");
        return key is not null;
    }

    // ---- Explorer classic context menu -----------------------------------------------------------
    protected Session OpenExplorer(string folderPath)
    {
        CloseExplorerFileWindows();
        var existingHandles = WindowsFinder.ListByApp("explorer")
            .Where(IsExplorerFileWindow)
            .Select(window => window.Hwnd)
            .ToHashSet();

        Step($"Opening Explorer at '{folderPath}'");
        using (Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/n,\"{folderPath}\"",
            UseShellExecute = true,
        }))
        {
        }

        var explorer = WindowsFinder.WaitForWindowByApp(
            "explorer",
            window => IsExplorerFileWindow(window) && !existingHandles.Contains(window.Hwnd),
            timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(explorer, $"Explorer did not open '{folderPath}'.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(explorer!.WindowHandle), WindowTimeoutMS, requiredConsecutiveMatches: 2),
            $"Explorer HWND {explorer.WindowHandle} did not become the stable foreground window.");
        return explorer;
    }

    protected Session OpenClassicContextMenu(Session explorer, string filePath)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(WindowTimeoutMS);
        do
        {
            KeyboardHelper.SendKeys(Key.Esc);
            var selection = ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(explorer.WindowHandle),
                new[] { filePath },
                focusedPath: filePath,
                timeoutMS: ActionTimeoutMS,
                requiredConsecutiveMatches: 4);
            if (!selection.Succeeded)
            {
                Thread.Sleep(300);
                continue;
            }

            if (!WindowControl.TryOpenContextMenuForFocusedControl(new IntPtr(explorer.WindowHandle)))
            {
                Thread.Sleep(300);
                continue;
            }

            var surface = WaitForMenuWindow(
                ContextMenuWindowClasses,
                ActionTimeoutMS);
            if (surface is null)
            {
                continue;
            }

            if (IsClassicMenuWindow(surface))
            {
                return surface;
            }

            var showMore = FindVisibleMenuItem(surface, "Show more options", timeoutMS: 8_000);
            if (showMore is null)
            {
                continue;
            }

            try
            {
                showMore.Invoke(msPostAction: 300);
            }
            catch (Exception)
            {
                // The transient modern menu can disappear before Invoke; reopen it on the next attempt.
                continue;
            }

            var classic = WaitForMenuWindow(ClassicContextMenuWindowClasses, ActionTimeoutMS);
            if (classic is not null)
            {
                return classic;
            }
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail(
            $"Explorer never opened the classic context menu for '{Path.GetFileName(filePath)}'. " +
            $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return null!;
    }

    protected Element? WaitForClassicContextMenuItem(Session explorer, string filePath, string caption, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            var menu = OpenClassicContextMenu(explorer, filePath);
            var item = FindExact<Element>(menu, caption, timeoutMS: 1_000);
            if (item is not null)
            {
                return item;
            }

            KeyboardHelper.SendKeys(Key.Esc);
            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private static Session? WaitForMenuWindow(IReadOnlyList<string> classNames, int timeoutMS) =>
        WindowsFinder.WaitForWindow(
            window => classNames.Any(name => name.Equals("#32768", StringComparison.OrdinalIgnoreCase)
                ? window.ClassName.Equals(name, StringComparison.OrdinalIgnoreCase)
                : window.ClassName.Contains(name, StringComparison.OrdinalIgnoreCase)),
            timeoutMS: timeoutMS,
            pollIntervalMS: 100);

    private static bool IsClassicMenuWindow(Session menu) =>
        WindowsFinder.ListAll().Any(window =>
            window.Hwnd == menu.WindowHandle &&
            window.ClassName.Equals("#32768", StringComparison.OrdinalIgnoreCase));

    private static Element? FindVisibleMenuItem(Session menu, string name, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            try
            {
                var item = menu.FindAll<Element>(By.Name(name), timeoutMS: 250)
                    .FirstOrDefault(element =>
                        element.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                        element.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) &&
                        element.Width > 0 &&
                        element.Height > 0 &&
                        element.Displayed);
                if (item is not null)
                {
                    return item;
                }
            }
            catch (Exception)
            {
                // The transient menu can disappear during a query; let the caller reopen it.
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    protected static bool IsExplorerFileWindow(WindowsFinder.WindowInfo window) =>
        window.ClassName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase);

    protected static bool CloseExplorerFileWindows() =>
        WindowControl.TryCloseByApp("explorer", IsExplorerFileWindow, timeoutMS: 10_000);

    // ---- Registry import and association ---------------------------------------------------------
    protected static bool CanConfirmRegistryImportInteractively =>
        !EnvironmentConfig.IsInPipeline &&
        Environment.UserInteractive &&
        !ElevationHelper.IsCurrentProcessElevated();

    /// <returns>
    /// <see langword="true"/> when the Registry Editor dialogs were automated; otherwise,
    /// <see langword="false"/> when an interactive local run must complete the elevated dialogs.
    /// </returns>
    protected bool ConfirmRegistryImport()
    {
        var confirmation = WindowsFinder.WaitForWindowByApp(
            "regedit",
            window => window.Title.Contains("Registry Editor", StringComparison.OrdinalIgnoreCase),
            timeoutMS: CanConfirmRegistryImportInteractively ? InteractiveRegistryImportTimeoutMS : WindowTimeoutMS);
        Assert.IsNotNull(confirmation, "Registry Editor did not display the import confirmation.");

        if (CanConfirmRegistryImportInteractively && confirmation!.IsElevated is not false)
        {
            LogManualRegistryImportInstructions();
            return false;
        }

        try
        {
            var yes = FindExact<Button>(confirmation!, "Yes", ActionTimeoutMS);
            Assert.IsNotNull(yes, "Registry Editor did not expose the import confirmation's Yes button.");
            yes!.Click(msPostAction: 500);

            var result = WindowsFinder.WaitForWindowByApp(
                "regedit",
                window =>
                    window.Hwnd != confirmation.WindowHandle &&
                    window.Title.Contains("Registry Editor", StringComparison.OrdinalIgnoreCase),
                timeoutMS: ActionTimeoutMS);
            Assert.IsNotNull(result, "Registry Editor did not display the import result.");

            var success = result!.FindAll<Element>(By.Name("successfully"), ActionTimeoutMS)
                .FirstOrDefault(element => element.Name.Contains("successfully", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(success, "Registry Editor did not report that the registry import completed successfully.");

            var ok = FindExact<Button>(result, "OK", ActionTimeoutMS);
            Assert.IsNotNull(ok, "Registry Editor did not report the import result.");
            ok!.Click(msPostAction: 300);
            return true;
        }
        catch (AssertFailedException ex) when (
            CanConfirmRegistryImportInteractively &&
            IsWinappTargetAccessFailure(ex.Message))
        {
            LogManualRegistryImportInstructions();
            return false;
        }
    }

    private void LogManualRegistryImportInstructions() =>
        Step(
            "Registry Editor is elevated beyond the local test host's UI-automation integrity level. " +
            "Choose Yes in Registry Editor and dismiss the result dialog; the test will wait for the isolated HKCU value.");

    private static bool IsWinappTargetAccessFailure(string message) =>
        message.Contains("No running app found", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("AppNotFoundException", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("access_denied", StringComparison.OrdinalIgnoreCase);

    protected static string? QueryRegFileExecutable()
    {
        uint length = 0;
        var result = AssocQueryString(0, AssocString.Executable, ".reg", "open", null, ref length);
        if (result != 1 || length == 0)
        {
            return null;
        }

        var value = new StringBuilder((int)length);
        result = AssocQueryString(0, AssocString.Executable, ".reg", "open", value, ref length);
        return result == 0 ? value.ToString() : null;
    }

    protected static string? QueryRegistryPreviewOpenCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\PowerToys.RegistryPreview\shell\open\command");
        return key?.GetValue(null) as string;
    }

    protected static bool IsDefaultAppRegistrationPresent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.reg\OpenWithProgIDs");
        return key?.GetValueNames().Contains("PowerToys.RegistryPreview", StringComparer.OrdinalIgnoreCase) == true;
    }

    protected static bool IsCurrentUserLocalAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        return identity.Groups?.Contains(administrators) == true;
    }

    protected static string NormalizeRegContent(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n');

    private static void AssertRegistryPreviewOwnsForeground(string operation)
    {
        var foreground = WindowControl.GetForegroundWindowInfo();
        Assert.IsTrue(
            foreground.ProcessName.Contains("RegistryPreview", StringComparison.OrdinalIgnoreCase),
            $"Registry Preview did not own the foreground {operation}. Foreground: {foreground}.");
    }

    private enum AssocString : uint
    {
        Executable = 2,
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint AssocQueryString(
        uint flags,
        AssocString str,
        string association,
        string? extra,
        StringBuilder? output,
        ref uint outputLength);
}
