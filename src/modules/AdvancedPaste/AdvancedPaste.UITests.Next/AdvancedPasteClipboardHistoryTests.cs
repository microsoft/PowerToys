// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Button = Microsoft.PowerToys.UITest.Next.Button;
using CheckBox = Microsoft.PowerToys.UITest.Next.CheckBox;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AdvancedPaste")]
[TestCategory("DestructiveClipboardHistory")]
public sealed class AdvancedPasteClipboardHistoryTests : AdvancedPasteTestBase
{
    private const string HistoryCard = "AdvancedPasteClipboardHistoryEnabledSettingsCard";
    private const string WindowsHistoryToggle = "SystemSettings_Clipboard_IsSaveClipboardItemsEnabled_ToggleSwitch";
    private const string ClipboardRegistryPath = @"Software\Microsoft\Clipboard";
    private const string ClipboardRegistryValue = "EnableClipboardHistory";
    private const int MaximumHistoryEntries = 25;

    private readonly string historyPrefix = $"PowerToys.AdvancedPaste.UITests.{Guid.NewGuid():N}";
    private readonly HashSet<string> originalHistoryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> fixtureContents = new(StringComparer.Ordinal);
    private readonly HashSet<string> inspectedHistoryIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ownedHistoryItems = new(StringComparer.Ordinal);

    // Each case restores the OS history setting. Do not carry the previous case's
    // ItemsView and asynchronous history notifications into the next OS baseline.
    protected override bool ReuseScopeAcrossTests => false;

    [TestMethod]
    public Task SelectingANonFirstEntryUpdatesClipboardWithoutChangingHistoryIdentity()
    {
        return RunHistoryScenarioAsync(
            entryCount: 3,
            requiresEmptyHistory: false,
            async () =>
            {
                var first = await CopyHistoryFixtureAsync("first");
                var second = await CopyHistoryFixtureAsync("second");
                var third = await CopyHistoryFixtureAsync("third");
                var before = await WaitForHistoryAsync(
                    items => items.Take(3).Select(item => item.Id).SequenceEqual([third.Id, second.Id, first.Id]),
                    "The three clipboard fixtures did not enter Windows history in reverse copy order.");
                Assert.AreNotEqual(second.Id, before[0].Id, "The selection fixture must not already be the first history item.");
                var expectedIds = before.Select(item => item.Id).ToArray();

                var history = OpenHistory();
                Step("Selecting the second Advanced Paste history entry without pasting it");
                FindHistoryItem(history, second.Content).Invoke(msPostAction: 0);
                WaitUntil(
                    () => ReadClipboardText() == second.Content,
                    "Selecting an Advanced Paste history entry did not put its exact text on the OS clipboard.");
                var after = await WaitForHistoryAsync(
                    items => items.Select(item => item.Id).Order(StringComparer.Ordinal).SequenceEqual(expectedIds.Order(StringComparer.Ordinal)),
                    "Selecting an existing entry did not preserve the exact Windows clipboard-history IDs.");

                CollectionAssert.AreEquivalent(
                    expectedIds,
                    after.Select(item => item.Id).ToArray(),
                    "Selecting an existing history entry created or removed a Windows history item.");
                var selected = after.Single(item => item.Id == second.Id);
                Assert.AreEqual(
                    second.Content,
                    await selected.Content.GetTextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)),
                    "The selected Windows history ID no longer refers to its original test content.");
                FindHistoryItem(history, second.Content);
                Assert.AreEqual(second.Content, ReadClipboardText(), "The selected history item did not remain the current clipboard content.");
                Assert.AreEqual(string.Empty, Target.Text, "Selecting a clipboard-history entry unexpectedly pasted it into the destination.");
                Assert.IsTrue(IsAdvancedPasteVisible(), "Selecting clipboard history unexpectedly closed Advanced Paste.");
            });
    }

    [TestMethod]
    public Task DeletingAnEntryRemovesOnlyItsExactWindowsHistoryId()
    {
        return RunHistoryScenarioAsync(
            entryCount: 2,
            requiresEmptyHistory: false,
            async () =>
            {
                var victim = await CopyHistoryFixtureAsync("delete");
                var survivor = await CopyHistoryFixtureAsync("keep");
                var before = await WaitForHistoryAsync(
                    items => items.Count >= 2 && items[0].Id == survivor.Id && items[1].Id == victim.Id,
                    "The deletion fixtures did not reach Windows clipboard history.");
                var expectedIds = before.Where(item => item.Id != victim.Id).Select(item => item.Id).ToArray();

                var history = OpenHistory();
                Step("Opening More options on the exact test-owned history entry");
                AdvancedPasteUi.Child<Button>(
                    history,
                    () => FindHistoryItem(history, victim.Content),
                    node => AdvancedPasteUi.Property(node, "automationId") == "ClipboardHistoryItemMoreOptionsButton",
                    "More options button for the test-owned deletion entry").Invoke(msPostAction: 0);

                Step("Deleting the selected history entry through the Advanced Paste menu");
                var delete = history.FindAll<Element>(By.Name(ProductStrings.DeleteHistoryItem), 10_000)
                    .Where(element => element.Name == ProductStrings.DeleteHistoryItem && element.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.HasCount(1, delete, "The history entry's More options menu did not expose a unique Delete command.");
                delete[0].Invoke(msPostAction: 0);

                var after = await WaitForHistoryAsync(
                    items => items.All(item => item.Id != victim.Id),
                    "The entry disappeared from Advanced Paste but its exact ID remained in Windows clipboard history.");
                CollectionAssert.AreEquivalent(
                    expectedIds,
                    after.Select(item => item.Id).ToArray(),
                    "Deleting one Advanced Paste history item also removed or created another Windows history item.");
                WaitUntil(
                    () => HistoryItems(history, victim.Content).Length == 0,
                    "The deleted entry remained in the Advanced Paste history flyout.",
                    shouldRetryException: AdvancedPasteUi.IsStaleElement);
                FindHistoryItem(history, survivor.Content);
                Assert.AreEqual(survivor.Content, ReadClipboardText(), "Deleting a non-current history entry changed the current clipboard.");
                Assert.AreEqual(string.Empty, Target.Text, "Deleting history unexpectedly pasted content.");
            });
    }

    [TestMethod]
    public Task DisablingClipboardHistoryThroughSettingsHidesHistoryAccess()
    {
        return RunHistoryScenarioAsync(
            entryCount: 1,
            requiresEmptyHistory: true,
            async () =>
            {
                await CopyHistoryFixtureAsync("before-disable");
                var before = OpenAdvancedPaste();
                Assert.IsTrue(before.Find<Button>(By.Name(ProductStrings.ClipboardHistory), 15_000).IsEnabled, "Clipboard history was not available before disabling it.");
                DismissAdvancedPaste();

                Step("Disabling OS clipboard history through the real Advanced Paste Settings checkbox");
                var checkbox = HistoryCheckbox();
                Assert.IsTrue(checkbox.IsChecked, "The history Settings checkbox was not initially checked.");
                Assert.IsTrue(checkbox.IsEnabled, "BLOCKED: the history Settings checkbox cannot be edited even though OS history is enabled.");
                checkbox.Invoke(msPostAction: 0);
                Assert.IsTrue(HistoryCheckbox().WaitForProperty("ToggleState", "Off", 10_000), "The history Settings checkbox did not become unchecked.");
                WaitUntil(
                    () => !WinClipboard.IsHistoryEnabled(),
                    "Disabling history in Settings did not disable the Windows clipboard-history service.",
                    timeoutMS: 20_000);
                using (var key = Registry.CurrentUser.OpenSubKey(ClipboardRegistryPath))
                {
                    Assert.IsNotNull(key, "Settings did not persist the Windows clipboard-history preference.");
                    Assert.AreEqual(0, key.GetValue(ClipboardRegistryValue), "Settings did not persist EnableClipboardHistory=0.");
                }

                Step("Copying fresh test-owned text after disabling history");
                var content = $"{historyPrefix}.after-disable";
                fixtureContents.Add(content);
                SetClipboardText(content);
                var window = OpenAdvancedPaste();
                Step("Checking the current product contract: history access is hidden, not merely disabled");
                WaitUntil(
                    () => !window.Has<Button>(By.Name(ProductStrings.ClipboardHistory), 0),
                    "The Advanced Paste history button remained available while OS clipboard history was disabled.");
                Assert.IsTrue(window.Has(By.AccessibilityId("PasteOptionsListView")), "The module did not finish opening with history disabled.");
                window.Find<TextBlock>(By.Name(content), 15_000);
                Assert.AreEqual(content, ReadClipboardText(), "The current clipboard was not usable after disabling history.");
                Assert.IsFalse(WinClipboard.IsHistoryEnabled(), "Copying fresh text or opening Advanced Paste unexpectedly re-enabled OS history.");
                Assert.AreEqual(string.Empty, Target.Text, "Disabling clipboard history pasted content.");
            });
    }

    [TestMethod]
    public Task FullHistoryCanBeResetBeforeAddingNewFixtureEntries()
    {
        return RunHistoryScenarioAsync(
            entryCount: MaximumHistoryEntries,
            requiresEmptyHistory: true,
            async () =>
            {
                for (var index = 0; index < MaximumHistoryEntries; index++)
                {
                    await CopyHistoryFixtureAsync($"capacity-{index}");
                }

                var full = await WaitForHistoryAsync(
                    items => items.Count == MaximumHistoryEntries,
                    "The capacity regression did not fill Windows clipboard history.");
                var previousIds = full.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                var baseline = await EnsureHistorySpaceAsync(entryCount: 1, requiresEmptyHistory: true);
                Assert.IsEmpty(baseline, "The history fixture did not establish an empty baseline.");

                var fresh = await CopyHistoryFixtureAsync("after-capacity-reset");
                var after = await WaitForHistoryAsync(
                    items => items.Count == 1 && items[0].Id == fresh.Id,
                    "The new fixture did not become the only item after clearing full history.");
                Assert.IsFalse(
                    after.Any(item => previousIds.Contains(item.Id)),
                    "An entry from the cleared history reappeared in the new baseline.");
            });
    }

    [TestMethod]
    public Task RepeatedHistoryEnableDisableCapturesNewEntries()
    {
        return RunHistoryScenarioAsync(
            entryCount: 3,
            requiresEmptyHistory: true,
            async () =>
            {
                var empty = await CaptureWindowsHistoryEvidenceAsync("empty-baseline");
                Assert.AreEqual("Empty", empty.State, "Win+V did not show its explicit empty-history state for the empty Windows API baseline.");
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    var fixture = await CopyHistoryFixtureAsync($"enable-cycle-{cycle}");
                    var items = await ReadHistoryAsync();
                    Assert.IsTrue(items.Any(item => item.Id == fixture.Id), "The enabled Windows history did not retain the copied fixture.");
                    var surface = await CaptureWindowsHistoryEvidenceAsync($"enable-cycle-{cycle}");
                    CollectionAssert.Contains(surface.VisibleFixtureContents, fixture.Content, "Win+V did not expose the exact newly copied fixture through UIA.");
                    SetWindowsHistoryEnabled(false);
                    Assert.IsFalse(WinClipboard.IsHistoryEnabled(), "Windows history remained enabled after the OS Settings toggle was disabled.");
                    SetWindowsHistoryEnabled(true);
                }

                await CopyHistoryFixtureAsync("after-repeated-enable");
            });
    }

    private async Task RunHistoryScenarioAsync(int entryCount, bool requiresEmptyHistory, Func<Task> scenario)
    {
        var originalRegistry = ClipboardHistoryRegistrySnapshot.Capture(Target.Invoke(WinClipboard.IsHistoryEnabled));
        try
        {
            Step("Enabling OS history through Windows Settings; a registry preference alone does not activate history capture");
            EnableHistoryFixture();
            var originalItems = await EnsureHistorySpaceAsync(entryCount, requiresEmptyHistory);
            originalHistoryIds.UnionWith(originalItems.Select(item => item.Id));

            Step("Reloading the Advanced Paste Settings page after the OS fixture reaches its enabled state");
            NavigateToGeneralSettings();
            NavigateToSettings();
            Assert.IsTrue(HistoryCheckbox().IsEnabled, "BLOCKED: the OS history fixture is enabled, but the Advanced Paste history checkbox is disabled.");
            Assert.IsTrue(HistoryCheckbox().IsChecked, "Settings did not reflect the enabled OS history fixture.");
            await scenario();
        }
        catch (Exception failure)
        {
            Step($"History scenario failed before diagnostics: {failure}");
            CaptureHistoryDesktop("before-winv");
            try
            {
                await CaptureWindowsHistoryEvidenceAsync("failure");
            }
            catch (Exception diagnosticFailure)
            {
                Step($"Windows history diagnostics failed; preserving the original test failure: {diagnosticFailure}");
            }

            try
            {
                LogHistoryServiceDiagnostics();
            }
            catch (Exception diagnosticFailure)
            {
                Step($"Clipboard-broker diagnostics failed; preserving the original test failure: {diagnosticFailure}");
            }

            await CaptureFailureArtifactsAsync();
            throw;
        }
        finally
        {
            Step("Removing only this test's history IDs and restoring the original OS history preference");
            try
            {
                DismissAdvancedPaste();
                await RemoveOwnedHistoryAsync();
            }
            finally
            {
                originalRegistry.Restore(SetWindowsHistoryEnabled);
            }
        }
    }

    private async Task<ClipboardHistoryItem[]> EnsureHistorySpaceAsync(int entryCount, bool requiresEmptyHistory)
    {
        bool IsReady(IReadOnlyList<ClipboardHistoryItem> items) =>
            items.Count + entryCount <= MaximumHistoryEntries && (!requiresEmptyHistory || items.Count == 0);

        var items = await ReadHistoryAsync();
        if (IsReady(items))
        {
            return items;
        }

        Step($"WARNING: clearing unpinned Windows clipboard history ({items.Length} saved entries) to prepare this destructive history scenario. Cleared entries cannot be restored.");
        Assert.IsTrue(
            Target.Invoke(WinClipboard.ClearHistory),
            "Windows rejected the clipboard-history clear operation; the history scenario cannot proceed.");
        var message = requiresEmptyHistory
            ? "BLOCKED: Windows history is not empty after clearing. Clear history preserves pinned items; unpin or remove them before running this scenario."
            : $"BLOCKED: Windows history still has insufficient capacity for {entryCount} fixtures after clearing. Pinned items are preserved.";
        return await WaitForHistoryAsync(IsReady, message);
    }

    private CheckBox HistoryCheckbox() => AdvancedPasteUi.CardControl<CheckBox>(SettingsSession, HistoryCard, "CheckBox");

    private void EnableHistoryFixture() => SetWindowsHistoryEnabled(true);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    private async Task<NativeHistorySurface> CaptureWindowsHistoryEvidenceAsync(string label)
    {
        var before = await ReadHistoryApiDiagnosticsAsync();
        var beforeSta = await Target.Invoke(ReadHistoryApiDiagnosticsAsync);
        Step($"Win+V '{label}' before opening: test-thread API {JsonSerializer.Serialize(before)}; pumping-STA API {JsonSerializer.Serialize(beforeSta)}");
        var existing = ReadWindowsHistorySurface();
        if (existing.Tree is null)
        {
            Step($"Opening native Windows history through Win+V for '{label}'; foreground: {WindowControl.GetForegroundWindowInfo()}");
            Target.Invoke(() => KeyboardHelper.SendKeys(Key.LWin, Key.V));
        }

        try
        {
            var ready = WaitHelper.WaitForStable(
                ReadWindowsHistorySurface,
                surface => surface.Tree is not null && surface.State != "Unknown",
                timeoutMS: 20_000,
                requiredConsecutiveMatches: 2,
                shouldRetryException: AdvancedPasteUi.IsStaleElement);
            var surface = ready.LastObservation ?? existing;
            var after = await ReadHistoryApiDiagnosticsAsync();
            var afterSta = await Target.Invoke(ReadHistoryApiDiagnosticsAsync);
            var path = Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"winv-{label}-{Guid.NewGuid():N}.json");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    new
                    {
                        Before = before,
                        BeforeSta = beforeSta,
                        SurfaceReady = ready.Succeeded,
                        SurfaceError = ready.LastException?.ToString(),
                        Surface = surface,
                        ExpectedFixtureContents = fixtureContents.ToArray(),
                        After = after,
                        AfterSta = afterSta,
                        Foreground = WindowControl.GetForegroundWindowInfo().ToString(),
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(path);
            Step($"Win+V '{label}': ready={ready.Succeeded}; state={surface.State}; visible fixture count={surface.VisibleFixtureContents.Length}; after API {JsonSerializer.Serialize(after)}; evidence={path}");
            return surface;
        }
        finally
        {
            CaptureHistoryDesktop($"winv-{label}");
            var current = ReadWindowsHistorySurface();
            if (current.Tree is { } tree)
            {
                var close = AdvancedPasteUi.Nodes(tree).Where(node =>
                    AdvancedPasteUi.Property(node, "type") == "Button" &&
                    AdvancedPasteUi.Property(node, "automationId") is "TEMPLATE_PART_CloseButton" or "CloseButton").ToArray();
                Assert.IsTrue(close.Length <= 1, "The native Win+V panel exposed ambiguous Close buttons.");
                if (close.Length == 1)
                {
                    var width = close[0].GetProperty("width").GetInt32();
                    var height = close[0].GetProperty("height").GetInt32();
                    Assert.IsTrue(width > 0 && height > 0, "The native Win+V Close button did not have visible bounds.");
                    Step("Clicking the inspected native history panel's visible Close button");
                    MouseHelper.LeftClickAt(
                        close[0].GetProperty("x").GetInt32() + (width / 2),
                        close[0].GetProperty("y").GetInt32() + (height / 2));
                }
                else
                {
                    Step("Dismissing the caret-anchored Win+V panel, which has no Close button, with Escape");
                    Target.Invoke(() => KeyboardHelper.SendKeys(Key.Esc));
                }
            }
            else
            {
                Target.Invoke(() => KeyboardHelper.SendKeys(Key.Esc));
            }

            WaitUntil(
                () => ReadWindowsHistorySurface().Tree is null,
                "The native Win+V panel did not close before the next fixture interaction.",
                timeoutMS: 20_000);
        }
    }

    private NativeHistorySurface ReadWindowsHistorySurface()
    {
        // UWP's composed popup lives under an ApplicationFrameWindow in the desktop UIA tree.
        // Inspecting TextInputHost's CoreWindow directly can return only an empty, cloaked host.
        var tree = WinappCli.InvokeJson(
            "ui", "inspect", "-w", GetDesktopWindow().ToInt64().ToString(CultureInfo.InvariantCulture),
            "--json", "-d", "20", "--hide-offscreen");
        var windows = AdvancedPasteUi.Nodes(tree).Where(node =>
            AdvancedPasteUi.Property(node, "type") == "Window" &&
            AdvancedPasteUi.Property(node, "className") == "Windows.UI.Core.CoreWindow");
        foreach (var window in windows)
        {
            var nodes = AdvancedPasteUi.Nodes(window).ToArray();
            if (!nodes.Any(node => AdvancedPasteUi.Property(node, "automationId") is
                "TEMPLATE_PART_ClipboardTitleBar" or "navigation-menu-item-container-clipboard"))
            {
                continue;
            }

            var names = nodes.Select(node => AdvancedPasteUi.Property(node, "name")).ToArray();
            var state = names.Any(name => name == "Your clipboard is empty" || name.StartsWith("Nothing here,", StringComparison.Ordinal))
                ? "Empty"
                : nodes.Any(node =>
                    AdvancedPasteUi.Property(node, "automationId").StartsWith("item-ClipboardHistory-", StringComparison.Ordinal) ||
                    (AdvancedPasteUi.Property(node, "automationId") == "TEMPLATE_PART_ClipboardItemsList" &&
                        AdvancedPasteUi.Nodes(node).Any(child => AdvancedPasteUi.Property(child, "type") == "ListItem")))
                    ? "Populated"
                    : names.Any(name => name.Equals("Turn on", StringComparison.OrdinalIgnoreCase))
                        ? "Disabled"
                        : "Unknown";
            return new NativeHistorySurface(
                window.Clone(),
                state,
                fixtureContents.Where(content => names.Contains(content, StringComparer.Ordinal)).ToArray());
        }

        return new NativeHistorySurface(null, "Unavailable", []);
    }

    private async Task<HistoryApiObservation> ReadHistoryApiDiagnosticsAsync()
    {
        try
        {
            var enabled = WinClipboard.IsHistoryEnabled();
            var result = await WinClipboard.GetHistoryItemsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            return new HistoryApiObservation(enabled, result.Status.ToString(), result.Items.Select(item => item.Id).ToArray(), null);
        }
        catch (Exception failure)
        {
            Step($"Native clipboard-history API diagnostic failed: {failure}");
            return new HistoryApiObservation(null, "Error", [], failure.ToString());
        }
    }

    private void CaptureHistoryDesktop(string label)
    {
        var path = Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"{label}-{Guid.NewGuid():N}.png");
        if (ScreenCapture.TryCaptureDesktop(path))
        {
            TestContext.AddResultFile(path);
        }
        else
        {
            Step($"History diagnostic desktop capture failed: {path}");
        }
    }

    private sealed record NativeHistorySurface(JsonElement? Tree, string State, string[] VisibleFixtureContents);

    private sealed record HistoryApiObservation(bool? Enabled, string Status, string[] Ids, string? Error);

    private void LogHistoryServiceDiagnostics()
    {
        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        Assert.IsNotNull(services, "Clipboard-history service diagnostics could not read the service registry.");
        foreach (var name in services.GetSubKeyNames().Where(name => name.StartsWith("cbdhsvc", StringComparison.OrdinalIgnoreCase)))
        {
            var start = new ProcessStartInfo("sc.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("query");
            start.ArgumentList.Add(name);
            using var process = Process.Start(start);
            Assert.IsNotNull(process, "The clipboard-history service diagnostic process could not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill();
                Step($"Clipboard-history service diagnostic timed out for {name}.");
                continue;
            }

            Step($"Clipboard-history service {name}, exit {process.ExitCode}: {stdout.GetAwaiter().GetResult()}; {stderr.GetAwaiter().GetResult()}");
        }
    }

    private void SetWindowsHistoryEnabled(bool enabled)
    {
        Step($"Setting Windows clipboard history to {enabled} through its real OS Settings control");
        if (Target.Invoke(WinClipboard.IsHistoryEnabled) == enabled)
        {
            return;
        }

        var wasOpen = WindowsFinder.ListAll().Any(window =>
            window.Title == "Settings" && window.ProcessName is "ApplicationFrameHost" or "SystemSettings");
        using var launch = Process.Start(new ProcessStartInfo("ms-settings:clipboard") { UseShellExecute = true });
        Session? lastWindow = null;
        try
        {
            var timer = Stopwatch.StartNew();
            var lastClick = TimeSpan.FromSeconds(-2);
            var result = WaitHelper.WaitForStable(
                () =>
                {
                    var historyEnabled = Target.Invoke(WinClipboard.IsHistoryEnabled);
                    var window = WindowsFinder.WaitForWindow(
                        window => window.Title == "Settings" && window.ProcessName is "ApplicationFrameHost" or "SystemSettings",
                        timeoutMS: 500);
                    lastWindow = window ?? lastWindow;
                    var toggle = window?.FindAll<ToggleSwitch>(By.AccessibilityId(WindowsHistoryToggle), 0).SingleOrDefault();
                    if (toggle is not null)
                    {
                        Assert.IsTrue(toggle.IsEnabled, "BLOCKED: Windows clipboard history cannot be changed through OS Settings. Check the clipboard-history policy.");
                    }

                    return (Window: window, Toggle: toggle, State: toggle?.GetProperty("ToggleState") ?? "<page not ready>", HistoryEnabled: historyEnabled);
                },
                state => state.HistoryEnabled == enabled,
                timeoutMS: 45_000,
                requiredConsecutiveMatches: 2,
                recover: state =>
                {
                    var oppositeState = enabled ? "Off" : "On";
                    if (state.Window is null || state.Toggle is null ||
                        !string.Equals(state.State, oppositeState, StringComparison.OrdinalIgnoreCase) ||
                        state.HistoryEnabled == enabled || timer.Elapsed - lastClick < TimeSpan.FromSeconds(2))
                    {
                        return;
                    }

                    Assert.IsTrue(
                        WindowControl.WaitForForeground(new IntPtr(state.Window.WindowHandle), timeoutMS: 15_000, requiredConsecutiveMatches: 2),
                        "Windows clipboard Settings did not become foreground before changing history.");
                    Step($"Windows history is still {state.State} in both UI and API; activating the focused toggle once");
                    state.Toggle.Focus();
                    Assert.IsTrue(state.Toggle.WaitForProperty("HasKeyboardFocus", "true", 5_000), "The OS history toggle did not receive keyboard focus.");
                    Target.Invoke(() => KeyboardHelper.SendChord(Key.Space));
                    lastClick = timer.Elapsed;
                },
                shouldRetryException: AdvancedPasteUi.IsStaleElement);
            Assert.IsTrue(
                result.Succeeded,
                $"Windows clipboard history did not become {enabled}. Last UI state: {result.LastObservation.State}; last API state: {result.LastObservation.HistoryEnabled}; last exception: {result.LastException}");
        }
        finally
        {
            if (!wasOpen && lastWindow is not null)
            {
                Assert.IsTrue(
                    WindowControl.TryCloseByApp(lastWindow.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    "The test-opened Windows Settings window could not be closed.");
            }
        }
    }

    private async Task<HistoryFixture> CopyHistoryFixtureAsync(string name)
    {
        var content = $"{historyPrefix}.{name}";
        fixtureContents.Add(content);
        Step($"Copying the '{name}' history fixture and waiting for its exact Windows history ID");
        Target.CopyText(content);
        WaitUntil(() => ReadClipboardText() == content, "The real editor copy did not put the exact history fixture on the current clipboard.");
        Target.Clear();

        var items = await WaitForHistoryAsync(
            history => history.Count(item => ownedHistoryItems.TryGetValue(item.Id, out var text) && text == content) == 1,
            $"The '{name}' fixture did not reach Windows clipboard history before the next copy.");
        var item = items.Single(item => ownedHistoryItems.TryGetValue(item.Id, out var text) && text == content);
        return new HistoryFixture(item.Id, content);
    }

    private Session OpenHistory()
    {
        var window = OpenAdvancedPaste();
        Step("Opening the Advanced Paste clipboard-history flyout");
        var ready = WaitHelper.WaitForStable(
            () => window.Find<Button>(By.Name(ProductStrings.ClipboardHistory), 15_000),
            button => button is not null && button.IsEnabled && !button.IsOffscreen && button.Width > 0 && button.Height > 0,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        Assert.IsTrue(ready.Succeeded, "The clipboard-history button did not become visible and enabled.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            "Advanced Paste did not acquire foreground before opening its history.");
        ready.LastObservation!.Click(msPostAction: 0);
        return Microsoft.PowerToys.UITest.Next.Session.FromProcess(ProcessName);
    }

    private static Element[] HistoryItems(Session session, string content) =>
        session.FindAll<Element>(By.Name(content), 0)
            .Where(element => element.Name == content && element.ClassName.EndsWith("ItemContainer", StringComparison.Ordinal))
            .ToArray();

    private static Element FindHistoryItem(Session session, string content)
    {
        var result = WaitHelper.WaitForStable(
            () => HistoryItems(session, content),
            items => items is { Length: 1 },
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        Assert.IsTrue(result.Succeeded, $"Advanced Paste did not expose a unique test-owned history ItemContainer. Last matching count: {result.LastObservation?.Length}.");
        return result.LastObservation![0];
    }

    private static async Task<ClipboardHistoryItem[]> ReadHistoryAsync()
    {
        var result = await WinClipboard.GetHistoryItemsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(
            ClipboardHistoryItemsResultStatus.Success,
            result.Status,
            $"BLOCKED: Windows clipboard history could not be queried (status {result.Status}); authoritative history verification is required.");
        return result.Items.ToArray();
    }

    private async Task<ClipboardHistoryItem[]> WaitForHistoryAsync(
        Func<IReadOnlyList<ClipboardHistoryItem>, bool> matches,
        string message,
        Action<IReadOnlyList<ClipboardHistoryItem>>? recover = null)
    {
        var timer = Stopwatch.StartNew();
        var consecutiveMatches = 0;
        var lastCount = 0;
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            var items = await ReadHistoryAsync();
            await DiscoverOwnedHistoryItemsAsync(items);
            lastCount = items.Length;
            consecutiveMatches = matches(items) ? consecutiveMatches + 1 : 0;
            if (consecutiveMatches >= 2)
            {
                return items;
            }

            if (consecutiveMatches == 0)
            {
                recover?.Invoke(items);
            }

            await Task.Delay(100);
        }

        Assert.Fail($"{message} Last Windows history item count: {lastCount}; known test-owned IDs: {ownedHistoryItems.Count}.");
        return [];
    }

    private async Task DiscoverOwnedHistoryItemsAsync(IEnumerable<ClipboardHistoryItem> items)
    {
        if (fixtureContents.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            if (originalHistoryIds.Contains(item.Id) || !inspectedHistoryIds.Add(item.Id) || !item.Content.Contains(StandardDataFormats.Text))
            {
                continue;
            }

            var text = await item.Content.GetTextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            if (fixtureContents.Contains(text))
            {
                ownedHistoryItems.Add(item.Id, text);
            }
        }
    }

    private async Task RemoveOwnedHistoryAsync()
    {
        if (fixtureContents.Count == 0)
        {
            return;
        }

        // Clear our current content before re-enabling history, otherwise Windows can
        // asynchronously capture it as a new ID after the first cleanup snapshot.
        if (fixtureContents.Contains(ReadClipboardText()))
        {
            Assert.IsTrue(ClipboardHelper.Clear(), "The test-owned current clipboard content could not be cleared.");
        }

        EnableHistoryFixture();
        var deletedIds = new HashSet<string>(StringComparer.Ordinal);
        var remaining = await WaitForHistoryAsync(
            history => history.All(item => !ownedHistoryItems.ContainsKey(item.Id)),
            "Test-owned clipboard-history IDs remained after cleanup.",
            recover: history =>
            {
                foreach (var item in history.Where(item => ownedHistoryItems.ContainsKey(item.Id)))
                {
                    if (!deletedIds.Contains(item.Id))
                    {
                        if (Target.Invoke(() => WinClipboard.DeleteItemFromHistory(item)))
                        {
                            deletedIds.Add(item.Id);
                        }
                        else
                        {
                            Step($"Windows did not yet delete test-owned history ID '{item.Id}'; cleanup will recheck it.");
                        }
                    }
                }
            });
        Assert.IsTrue(
            originalHistoryIds.IsSubsetOf(remaining.Select(item => item.Id)),
            "An unrelated original Windows history item was lost during the scenario.");
    }

    private sealed record HistoryFixture(string Id, string Content);

    private sealed record ClipboardHistoryRegistrySnapshot(bool KeyExisted, bool ValueExisted, object? Value, RegistryValueKind Kind, bool HistoryEnabled)
    {
        internal static ClipboardHistoryRegistrySnapshot Capture(bool historyEnabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(ClipboardRegistryPath);
            var valueExisted = key?.GetValueNames().Contains(ClipboardRegistryValue, StringComparer.OrdinalIgnoreCase) == true;
            return new ClipboardHistoryRegistrySnapshot(
                key is not null,
                valueExisted,
                valueExisted ? key!.GetValue(ClipboardRegistryValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null,
                valueExisted ? key!.GetValueKind(ClipboardRegistryValue) : RegistryValueKind.Unknown,
                historyEnabled);
        }

        internal void Restore(Action<bool> setWindowsHistoryEnabled)
        {
            setWindowsHistoryEnabled(HistoryEnabled);
            using (var key = Registry.CurrentUser.CreateSubKey(ClipboardRegistryPath, writable: true))
            {
                Assert.IsNotNull(key, "The original Windows clipboard-history preference could not be restored.");
                if (ValueExisted)
                {
                    key.SetValue(ClipboardRegistryValue, Value!, Kind);
                }
                else
                {
                    key.DeleteValue(ClipboardRegistryValue, throwOnMissingValue: false);
                }

                key.Flush();
            }

            if (!KeyExisted)
            {
                bool empty;
                using (var key = Registry.CurrentUser.OpenSubKey(ClipboardRegistryPath))
                {
                    empty = key is { ValueCount: 0, SubKeyCount: 0 };
                }

                if (empty)
                {
                    Registry.CurrentUser.DeleteSubKey(ClipboardRegistryPath, throwOnMissingSubKey: false);
                }
            }

            WaitUntil(
                () => WinClipboard.IsHistoryEnabled() == HistoryEnabled,
                "Windows clipboard history did not return to its original enabled state.",
                timeoutMS: 20_000);
            using var restored = Registry.CurrentUser.OpenSubKey(ClipboardRegistryPath);
            var restoredValueExists = restored?.GetValueNames().Contains(ClipboardRegistryValue, StringComparer.OrdinalIgnoreCase) == true;
            Assert.AreEqual(ValueExisted, restoredValueExists, "The original clipboard-history registry value's presence was not restored.");
            if (ValueExisted)
            {
                Assert.AreEqual(Kind, restored!.GetValueKind(ClipboardRegistryValue), "The clipboard-history registry value's original kind was not restored.");
                var restoredValue = restored.GetValue(ClipboardRegistryValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (Value is Array expected && restoredValue is Array actual)
                {
                    CollectionAssert.AreEqual(expected, actual, "The original clipboard-history registry value was not restored.");
                }
                else
                {
                    Assert.AreEqual(Value, restoredValue, "The original clipboard-history registry value was not restored.");
                }
            }
        }
    }
}
