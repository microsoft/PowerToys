// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// Shared fixture for the PowerRename suites: a deterministic module baseline, temporary rename
/// fixtures, and the launch/drive/assert helpers every test needs.
/// </summary>
/// <remarks>
/// The scope is the Settings/runner scope because the runner owns module enablement and therefore
/// the context-menu registration. The PowerRename window itself is launched per test with its item
/// list on the command line, which is how the shell extension starts it for a user selection.
/// </remarks>
public abstract class PowerRenameTestBase : UITestBase
{
    protected const string PowerRenameProcessName = "PowerToys.PowerRename";
    protected const string ContextMenuCaption = "Rename with PowerRename";
    protected const string SearchBoxAutomationId = "textBox_search";
    protected const string ReplaceBoxAutomationId = "textBox_replace";
    protected const string ApplyButtonAutomationId = "button_rename";
    protected const string RegularExpressionsAutomationId = "checkBox_regex";
    protected const string MatchAllOccurrencesAutomationId = "checkBox_matchAll";
    protected const string CaseSensitiveAutomationId = "checkBox_case";
    protected const string OriginalCountAutomationId = "OriginalCount";
    protected const string RenamedCountAutomationId = "RenamedCount";
    protected const int WindowTimeoutMS = 30_000;
    protected const int PreviewTimeoutMS = 30_000;
    protected const int RenameTimeoutMS = 30_000;

    private static readonly string[] PowerRenameModuleOnly = { "PowerRename" };

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    // Everything PowerRename persists under its settings folder. A test that changes any of it must
    // hand the profile back exactly as it found it.
    private static readonly string[] ModuleStateFileNames =
    {
        "power-rename-settings.json",
        "power-rename-last-run-data.json",
        "power-rename-ui-flags",
        "search-mru.json",
        "replace-mru.json",
    };

    private readonly List<string> temporaryFolders = new();
    private readonly Dictionary<string, byte[]?> moduleStateSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> lastAppliedText = new(StringComparer.Ordinal);

    protected PowerRenameTestBase()
        : base(PowerToysModule.PowerToysSettings, WindowSize.UnSpecified, PowerRenameModuleOnly)
    {
        // Snapshot before the base class launches the runner, so the profile's original PowerRename
        // state survives even the very first test in the class.
        foreach (var fileName in ModuleStateFileNames)
        {
            var path = Path.Combine(ModuleSettingsDirectory, fileName);
            moduleStateSnapshot[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }

    protected override bool ReuseScopeAcrossTests => true;

    protected override IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        PowerRenameProcessName,
    };

    /// <summary><c>%LocalAppData%\Microsoft\PowerToys\PowerRename</c>.</summary>
    protected static string ModuleSettingsDirectory { get; } =
        Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "PowerRename");

    protected static bool IsWindows11OrNewer => Environment.OSVersion.Version.Build >= 22_000;

    [TestInitialize]
    public void PreparePowerRenameBaseline()
    {
        // The harness seeds the enabled-module baseline once per class. PowerRename's shell
        // extension re-reads settings.json on every context-menu query, so re-assert it per test.
        EnsureModuleEnabledInGlobalSettings();
        ConfigureModuleSettings(
            showIcon: true,
            extendedContextMenuOnly: false,
            persistState: false,
            mruEnabled: false,
            maxMruSize: 10,
            useBoostLib: false);
        ClearPersistedRenameState();
    }

    [TestCleanup]
    public async Task CleanupPowerRenameTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        var failures = new List<Exception>();
        try
        {
            ClosePowerRenameWindows();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        failures.AddRange(RestoreModuleState());

        foreach (var folder in temporaryFolders)
        {
            if (!TryDeleteDirectory(folder))
            {
                failures.Add(new IOException($"Cleanup could not delete temporary folder '{folder}'."));
            }
        }

        temporaryFolders.Clear();
        if (failures.Count > 0)
        {
            throw new AggregateException("PowerRename test cleanup failed.", failures);
        }
    }

    /// <summary>Timestamped trace so a CI hang names the step it stuck on.</summary>
    protected void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    // ---- fixtures -----------------------------------------------------------------------------

    protected string CreateTestFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PowerToys-PowerRename-UITests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        temporaryFolders.Add(folder);
        return folder;
    }

    protected static string CreateFile(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "PowerRename UI test fixture.");
        Assert.IsTrue(File.Exists(path), $"Fixture file was not written to disk at '{path}'.");
        return path;
    }

    protected static string CreateSubFolder(string parent, string folderName)
    {
        var path = Path.Combine(parent, folderName);
        Directory.CreateDirectory(path);
        Assert.IsTrue(Directory.Exists(path), $"Fixture folder was not created at '{path}'.");
        return path;
    }

    /// <summary>Entry names directly under <paramref name="folder"/>, ordered for stable comparison.</summary>
    /// <remarks>Ordinal ordering on purpose: a case-only rename is a real PowerRename scenario.</remarks>
    protected static IReadOnlyList<string> EntryNames(string folder) =>
        Directory.EnumerateFileSystemEntries(folder)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    // ---- module settings ----------------------------------------------------------------------

    /// <summary>
    /// Write PowerRename's own <c>power-rename-settings.json</c>. Both the shell extension (hosted in
    /// Explorer) and the UI reload it from disk, so seeding the file is enough — no runner restart.
    /// </summary>
    protected static void ConfigureModuleSettings(
        bool? showIcon = null,
        bool? extendedContextMenuOnly = null,
        bool? persistState = null,
        bool? mruEnabled = null,
        int? maxMruSize = null,
        bool? useBoostLib = null)
    {
        var path = Path.Combine(ModuleSettingsDirectory, "power-rename-settings.json");
        var settings = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        foreach (var (name, value) in new (string Name, JsonNode? Value)[]
        {
            ("ShowIcon", showIcon.HasValue ? JsonValue.Create(showIcon.Value) : null),
            ("ExtendedContextMenuOnly", extendedContextMenuOnly.HasValue ? JsonValue.Create(extendedContextMenuOnly.Value) : null),
            ("PersistState", persistState.HasValue ? JsonValue.Create(persistState.Value) : null),
            ("MRUEnabled", mruEnabled.HasValue ? JsonValue.Create(mruEnabled.Value) : null),
            ("MaxMRUSize", maxMruSize.HasValue ? JsonValue.Create(maxMruSize.Value) : null),
            ("UseBoostLib", useBoostLib.HasValue ? JsonValue.Create(useBoostLib.Value) : null),
        })
        {
            if (value is not null)
            {
                settings[name] = value;
            }
        }

        Directory.CreateDirectory(ModuleSettingsDirectory);
        File.WriteAllText(path, settings.ToJsonString(IndentedJson));
    }

    /// <summary>Drop the persisted flags, last-run text, and MRU lists so a launch starts from defaults.</summary>
    protected static void ClearPersistedRenameState()
    {
        foreach (var fileName in new[]
                 {
                     "power-rename-last-run-data.json",
                     "power-rename-ui-flags",
                     "search-mru.json",
                     "replace-mru.json",
                 })
        {
            var path = Path.Combine(ModuleSettingsDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Turn PowerRename on in the global settings, writing only when it is not already on.</summary>
    private static void EnsureModuleEnabledInGlobalSettings()
    {
        var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        if (root["enabled"] is not JsonObject enabled)
        {
            enabled = new JsonObject();
            root["enabled"] = enabled;
        }

        if (enabled["PowerRename"] is JsonValue value && value.TryGetValue<bool>(out var isEnabled) && isEnabled)
        {
            return;
        }

        enabled["PowerRename"] = true;
        Directory.CreateDirectory(SettingsConfigHelper.PowerToysSettingsRoot);
        File.WriteAllText(path, root.ToJsonString(IndentedJson));
    }

    // ---- PowerRename window --------------------------------------------------------------------

    /// <summary>
    /// Launch the PowerRename window over <paramref name="paths"/> and wait until its item list is
    /// populated. The item list is passed on the command line, matching how the shell extension
    /// starts the UI for an Explorer selection.
    /// </summary>
    /// <remarks>
    /// The returned session is process-scoped: a WinUI desktop app owns several top-level HWNDs, and
    /// its flyouts and combo-box drop-downs live in windows of their own, so a single-HWND scope would
    /// see only part of the UI.
    /// </remarks>
    protected Session LaunchPowerRename(params string[] paths)
    {
        Assert.IsTrue(paths.Length > 0, "PowerRename needs at least one item to rename.");
        ClosePowerRenameWindows();

        var exePath = SessionHelper.GetExecutablePath(PowerToysModule.PowerRename);
        Assert.IsTrue(File.Exists(exePath), $"PowerRename UI executable not found at '{exePath}'.");

        Step($"Launching PowerRename with {paths.Length} item(s) from '{Path.GetDirectoryName(paths[0])}'");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = true,
        };
        foreach (var path in paths)
        {
            startInfo.ArgumentList.Add(path);
        }

        using (Process.Start(startInfo) ?? throw new InvalidOperationException($"Process.Start returned null for '{exePath}'."))
        {
        }

        var window = WindowsFinder.WaitForWindowByApp(
            PowerRenameProcessName,
            info => info.Width > 0 && info.Height > 0,
            timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(window, "The PowerRename window did not appear after launching it with the fixture items.");

        Step("Waiting for the PowerRename window to become ready");
        var session = Session.FromProcess(PowerRenameProcessName, PowerToysModule.PowerRename, timeoutMS: WindowTimeoutMS);
        Assert.IsTrue(
            session.WaitFor(
                () => session.Has(By.AccessibilityId(SearchBoxAutomationId), timeoutMS: 1_000),
                WindowTimeoutMS,
                pollIntervalMS: 500),
            $"The PowerRename window did not expose its search box. {DescribeSurface(session)}");

        lastAppliedText.Clear();
        TryBringPowerRenameForward();

        var firstItemName = Path.GetFileName(paths[0].TrimEnd(Path.DirectorySeparatorChar));
        Assert.IsTrue(
            session.WaitFor(() => FindRowCheckBox(session, firstItemName) is not null, PreviewTimeoutMS, pollIntervalMS: 250),
            $"The PowerRename item list never showed '{firstItemName}'. {DescribeSurface(session)}");
        return session;
    }

    /// <summary>
    /// Best-effort raise of the PowerRename window. Foreground is not an authoritative readiness
    /// signal here — UIA search and invoke never need it, and <c>Element.Click</c> raises the window
    /// itself — while a scheduled interactive host can legitimately read <c>GetForegroundWindow()</c>
    /// as 0 for seconds at a time. Steps that do need real input verify their own effect instead.
    /// </summary>
    protected void TryBringPowerRenameForward()
    {
        var settled = WaitHelper.WaitForStable(
            observe: WindowControl.GetForegroundWindowInfo,
            isMatch: info => info.ProcessName.Contains("PowerRename", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 8_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250,
            recover: _ => WindowControl.TryFocusByApp(PowerRenameProcessName));

        if (!settled.Succeeded)
        {
            Step($"PowerRename did not take the foreground; continuing. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }
    }

    /// <summary>Window inventory plus a shallow UIA dump, so a readiness timeout says what was there.</summary>
    private static string DescribeSurface(Session session)
    {
        var windows = string.Join(
            "; ",
            WindowsFinder.ListByApp(PowerRenameProcessName)
                .Select(info => $"{info.ClassName} '{info.Title}' {info.Width}x{info.Height}"));

        string tree;
        try
        {
            tree = session.Inspect(depth: 8).ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            tree = $"<inspect failed: {ex.Message}>";
        }

        return $"Windows: [{windows}]. UIA tree: {tree[..Math.Min(tree.Length, 6_000)]}";
    }

    protected static void ClosePowerRenameWindows()
    {
        if (WindowControl.TryCloseByApp(PowerRenameProcessName, timeoutMS: 5_000) &&
            WaitForProcess(PowerRenameProcessName, expected: false, timeoutMS: 2_000))
        {
            return;
        }

        if (!WindowControl.TryKillProcessTreeByNameAndWait(PowerRenameProcessName, timeoutMS: 10_000) ||
            !WaitForProcess(PowerRenameProcessName, expected: false, timeoutMS: 2_000))
        {
            throw new InvalidOperationException($"Could not stop '{PowerRenameProcessName}' during test cleanup.");
        }
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

    // ---- driving the PowerRename UI --------------------------------------------------------------

    protected void SetSearchText(Session window, string text) =>
        SetAutoSuggestText(window, SearchBoxAutomationId, "search", text);

    protected void SetReplaceText(Session window, string text) =>
        SetAutoSuggestText(window, ReplaceBoxAutomationId, "replace", text);

    private void SetAutoSuggestText(Session window, string automationId, string description, string text)
    {
        Step($"Setting the {description} box to '{text}'");
        ApplyAutoSuggestText(window, automationId, description, text);
        lastAppliedText[automationId] = text;
    }

    private static void ApplyAutoSuggestText(Session window, string automationId, string description, string text)
    {
        window.Find<TextBox>(By.AccessibilityId(automationId), timeoutMS: PreviewTimeoutMS).SetText(text);

        // An empty box reports its placeholder through UIA, so only a non-empty value is verifiable.
        if (text.Length == 0)
        {
            return;
        }

        Assert.IsTrue(
            window.WaitFor(
                () => window.Find<TextBox>(By.AccessibilityId(automationId), timeoutMS: 1_000).Value == text,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            $"The {description} box did not accept the text '{text}'.");
    }

    /// <summary>
    /// Re-type the search and replace terms. A programmatic value change can reach the box but not the
    /// rename engine, leaving the preview computed from the previous terms; clearing first guarantees
    /// a fresh change notification, because re-setting the same value raises none.
    /// </summary>
    private void ReapplySearchAndReplaceText(Session window)
    {
        foreach (var (automationId, text) in lastAppliedText.ToList())
        {
            var description = automationId == SearchBoxAutomationId ? "search" : "replace";
            Step($"Re-applying the {description} box to nudge the rename engine");
            ApplyAutoSuggestText(window, automationId, description, string.Empty);
            ApplyAutoSuggestText(window, automationId, description, text);
        }
    }

    protected string GetSearchText(Session window) =>
        window.Find<TextBox>(By.AccessibilityId(SearchBoxAutomationId)).Value;

    protected string GetReplaceText(Session window) =>
        window.Find<TextBox>(By.AccessibilityId(ReplaceBoxAutomationId)).Value;

    protected void SetOptionCheckBox(Session window, string automationId, bool value)
    {
        Step($"Setting checkbox '{automationId}' to {value}");
        var checkBox = window.Find<CheckBox>(By.AccessibilityId(automationId), PreviewTimeoutMS);
        checkBox.SetCheck(value);
        Assert.IsTrue(
            checkBox.WaitForProperty("ToggleState", value ? "On" : "Off", timeoutMS: 5_000),
            $"The '{automationId}' checkbox did not settle to {(value ? "checked" : "unchecked")}.");
    }

    /// <summary>
    /// Press a toolbar <c>ToggleButton</c> only when its state is wrong — a blind press on an
    /// already-engaged toggle turns it off.
    /// </summary>
    protected void SetToggleButton(Session window, string automationId, bool value)
    {
        Step($"Setting toggle button '{automationId}' to {value}");
        var button = window.Find<Button>(By.AccessibilityId(automationId), PreviewTimeoutMS);
        if (GetToggleState(button) != value)
        {
            button.Invoke(msPostAction: 300);
        }

        Assert.IsTrue(
            button.WaitForProperty("ToggleState", value ? "On" : "Off", timeoutMS: 5_000),
            $"The '{automationId}' toggle button did not settle to {(value ? "on" : "off")}.");
    }

    protected static bool IsToggleButtonOn(Session window, string automationId)
    {
        var button = window.Find<Button>(By.AccessibilityId(automationId), timeoutMS: 5_000);
        return GetToggleState(button);
    }

    private static bool GetToggleState(Element element) =>
        string.Equals(element.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);

    /// <summary>Select an entry of the "Apply to" combo box; its popup lives in a separate window.</summary>
    protected void SelectApplyTo(Session window, string itemAutomationId)
    {
        Step($"Selecting rename scope '{itemAutomationId}'");
        var comboBox = window.Find<ComboBox>(By.AccessibilityId("comboBox_renameParts"), timeoutMS: PreviewTimeoutMS);
        comboBox.Invoke(msPostAction: 400);
        window.Find<Element>(By.AccessibilityId(itemAutomationId), timeoutMS: PreviewTimeoutMS)
            .Invoke(msPostAction: 400);
    }

    // ---- preview -------------------------------------------------------------------------------

    /// <summary>The per-item checkbox, whose UIA name is the item's original name.</summary>
    protected static CheckBox? FindRowCheckBox(Session window, string originalName, int timeoutMS = 2_000) =>
        FindExact<CheckBox>(window, originalName, timeoutMS);

    /// <summary>Include or exclude a single preview row, guarded on its current toggle state.</summary>
    protected void SetRowChecked(Session window, string originalName, bool value)
    {
        Step($"Setting the '{originalName}' row checkbox to {value}");
        var row = FindRowCheckBox(window, originalName, PreviewTimeoutMS);
        Assert.IsNotNull(row, $"The PowerRename item list did not contain a row for '{originalName}'.");
        if (row!.IsChecked != value)
        {
            row.Invoke(msPostAction: 300);
        }

        Assert.IsTrue(
            row.WaitForProperty("ToggleState", value ? "On" : "Off", timeoutMS: 5_000),
            $"The '{originalName}' row did not settle to {(value ? "checked" : "unchecked")}.");
    }

    /// <summary>Wait until the preview stops offering <paramref name="renamedName"/> for any row.</summary>
    protected void WaitForPreviewToDropName(Session window, string renamedName)
    {
        Step($"Waiting for the preview to drop '{renamedName}'");
        Assert.IsTrue(
            WaitForPreview(
                window,
                () => FindExact<TextBlock>(window, renamedName, timeoutMS: 500, comparison: StringComparison.Ordinal) is null),
            $"The PowerRename preview still showed '{renamedName}'. {DescribeSurface(window)}");
    }

    /// <summary>Wait until the preview column shows exactly <paramref name="renamedName"/> for some row.</summary>
    protected void WaitForPreviewName(Session window, string renamedName)
    {
        Step($"Waiting for the preview to show '{renamedName}'");
        Assert.IsTrue(
            WaitForPreview(
                window,
                () => FindExact<TextBlock>(window, renamedName, timeoutMS: 500, comparison: StringComparison.Ordinal) is not null),
            $"The PowerRename preview never showed '{renamedName}'. {DescribeSurface(window)}");
    }

    protected void WaitForOriginalCount(Session window, int count) =>
        WaitForCount(window, OriginalCountAutomationId, count, "original");

    /// <summary>Wait until the "will be renamed" badge reads <paramref name="count"/>.</summary>
    protected void WaitForRenamedCount(Session window, int count)
        => WaitForCount(window, RenamedCountAutomationId, count, "renamed");

    private void WaitForCount(Session window, string automationId, int count, string description)
    {
        var badge = "(" + count.ToString(CultureInfo.InvariantCulture) + ")";
        Step($"Waiting for the {description} count badge to read '{badge}'");
        Assert.IsTrue(
            WaitForPreview(
                window,
                () => window.FindAll<TextBlock>(By.AccessibilityId(automationId), timeoutMS: 500)
                    .Any(element => element.Name.Equals(badge, StringComparison.Ordinal))),
            $"The PowerRename header never reported {badge} {description} items. {DescribeSurface(window)}");
    }

    /// <summary>
    /// Poll a preview condition, re-typing the search and replace terms every few seconds so terms the
    /// rename engine never received are corrected instead of waited out.
    /// </summary>
    private bool WaitForPreview(Session window, Func<bool> isReady)
    {
        const int requiredConsecutiveMatches = 3;
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(PreviewTimeoutMS);
        var nextNudge = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        var consecutiveMatches = 0;
        while (true)
        {
            if (isReady())
            {
                if (++consecutiveMatches >= requiredConsecutiveMatches)
                {
                    return true;
                }
            }
            else
            {
                consecutiveMatches = 0;

                if (DateTime.UtcNow >= nextNudge && lastAppliedText.Count > 0)
                {
                    ReapplySearchAndReplaceText(window);
                    nextNudge = DateTime.UtcNow + TimeSpan.FromSeconds(6);
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(250);
        }
    }

    // ---- applying ------------------------------------------------------------------------------

    /// <summary>
    /// Press Apply and wait for the rename to land on disk. The Apply button is only enabled once at
    /// least one item will be renamed, so that is the readiness signal for the press.
    /// </summary>
    protected void ApplyRename(Session window)
    {
        Assert.IsTrue(
            window.WaitFor(() => FindApplyButton(window).IsEnabled, timeoutMS: PreviewTimeoutMS, pollIntervalMS: 250),
            "The Apply button never became enabled, so PowerRename had nothing to rename.");

        Step("Invoking Apply");
        FindApplyButton(window).Invoke(msPostAction: 500);
    }

    /// <summary>Apply, then wait until <paramref name="folder"/> contains exactly <paramref name="expectedNames"/>.</summary>
    protected void ApplyRenameAndAssertEntries(Session window, string folder, params string[] expectedNames)
    {
        ApplyRename(window);
        AssertEntries(folder, expectedNames);
    }

    protected void AssertEntries(string folder, params string[] expectedNames)
    {
        var expected = expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToList();
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(RenameTimeoutMS);
        IReadOnlyList<string> actual;
        do
        {
            actual = EntryNames(folder);
            if (actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                return;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail(
            $"'{folder}' contained [{string.Join(", ", actual)}] but [{string.Join(", ", expected)}] was expected " +
            $"within {RenameTimeoutMS}ms of pressing Apply.");
    }

    private static Element FindApplyButton(Session window) =>
        window.Find<Element>(By.AccessibilityId(ApplyButtonAutomationId), timeoutMS: PreviewTimeoutMS);

    // ---- lookup --------------------------------------------------------------------------------

    /// <summary>
    /// <c>By.Name</c> is a substring match in winappcli, so every lookup that must not collide with a
    /// longer caption goes through an exact-name filter.
    /// </summary>
    protected static T? FindExact<T>(
        Session session,
        string name,
        int timeoutMS = 5_000,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        where T : Element, new() =>
        session.FindAll<T>(By.Name(name), timeoutMS)
            .FirstOrDefault(element => element.Name.Equals(name, comparison));

    // ---- cleanup -------------------------------------------------------------------------------

    private IReadOnlyList<Exception> RestoreModuleState()
    {
        var failures = new List<Exception>();
        foreach (var (path, content) in moduleStateSnapshot)
        {
            try
            {
                if (content is null)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, content);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new IOException($"Could not restore PowerRename state file '{path}'.", ex));
            }
        }

        return failures;
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
            }
            catch (UnauthorizedAccessException)
            {
            }

            Thread.Sleep(250);
        }

        return !Directory.Exists(path);
    }
}
