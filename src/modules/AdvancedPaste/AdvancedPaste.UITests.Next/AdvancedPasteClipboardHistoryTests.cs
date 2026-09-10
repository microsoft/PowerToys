// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
public class AdvancedPasteClipboardHistoryTests : AdvancedPasteTestBase
{
    private const string HistoryCard = "AdvancedPasteClipboardHistoryEnabledSettingsCard";
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
                var delete = history.FindAll<Element>(By.Name("Delete"), 10_000)
                    .Where(element => element.Name == "Delete" && element.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase))
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
                Assert.IsTrue(before.Find<Button>(By.Name("Clipboard history"), 15_000).IsEnabled, "Clipboard history was not available before disabling it.");
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
                    () => !window.Has<Button>(By.Name("Clipboard history"), 0),
                    "The Advanced Paste history button remained available while OS clipboard history was disabled.");
                Assert.IsTrue(window.Has(By.AccessibilityId("PasteOptionsListView")), "The module did not finish opening with history disabled.");
                window.Find<TextBlock>(By.Name(content), 15_000);
                Assert.AreEqual(content, ReadClipboardText(), "The current clipboard was not usable after disabling history.");
                Assert.IsFalse(WinClipboard.IsHistoryEnabled(), "Copying fresh text or opening Advanced Paste unexpectedly re-enabled OS history.");
                Assert.AreEqual(string.Empty, Target.Text, "Disabling clipboard history pasted content.");
            });
    }

    private async Task RunHistoryScenarioAsync(int entryCount, bool requiresEmptyHistory, Func<Task> scenario)
    {
        var originalRegistry = ClipboardHistoryRegistrySnapshot.Capture();
        try
        {
            Step("Preparing OS history as a fixture; the Advanced Paste checkbox cannot enable an OS-disabled history card");
            EnableHistoryFixture();
            var originalItems = await ReadHistoryAsync();
            originalHistoryIds.UnionWith(originalItems.Select(item => item.Id));
            Assert.IsTrue(
                originalItems.Length + entryCount <= MaximumHistoryEntries,
                $"BLOCKED: Windows history contains {originalItems.Length} existing items and has no safe capacity for {entryCount} fixtures. No unrelated history will be evicted or cleared.");
            Assert.IsTrue(
                !requiresEmptyHistory || originalItems.Length == 0,
                "BLOCKED: the OS-history disable scenario requires an initially empty history so turning it off cannot discard unrelated entries.");

            Step("Reloading the Advanced Paste Settings page after the OS fixture reaches its enabled state");
            NavigateToGeneralSettings();
            NavigateToSettings();
            Assert.IsTrue(HistoryCheckbox().IsEnabled, "BLOCKED: the OS history fixture is enabled, but the Advanced Paste history checkbox is disabled.");
            Assert.IsTrue(HistoryCheckbox().IsChecked, "Settings did not reflect the enabled OS history fixture.");
            await scenario();
        }
        catch
        {
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
                originalRegistry.Restore();
            }
        }
    }

    private CheckBox HistoryCheckbox() => AdvancedPasteUi.CardControl<CheckBox>(Session, HistoryCard, "CheckBox");

    private static void EnableHistoryFixture()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(ClipboardRegistryPath, writable: true))
        {
            Assert.IsNotNull(key, "BLOCKED: the current user's clipboard-history preference cannot be opened.");
            key.SetValue(ClipboardRegistryValue, 1, RegistryValueKind.DWord);
            key.Flush();
        }

        WaitUntil(
            WinClipboard.IsHistoryEnabled,
            "BLOCKED: Windows Clipboard.IsHistoryEnabled never became true after enabling the fixture. Check OS clipboard-history policy and service readiness; no test is skipped.",
            timeoutMS: 20_000);
    }

    private async Task<HistoryFixture> CopyHistoryFixtureAsync(string name)
    {
        var content = $"{historyPrefix}.{name}";
        fixtureContents.Add(content);
        Step($"Copying the '{name}' history fixture and waiting for its exact Windows history ID");
        var package = new DataPackage();
        package.SetText(content);
        Target.Invoke(() =>
        {
            Assert.IsTrue(
                WinClipboard.SetContentWithOptions(package, new ClipboardContentOptions { IsAllowedInHistory = true, IsRoamable = false }),
                "Windows rejected the test clipboard content.");
            WinClipboard.Flush();
        });
        Assert.AreEqual(content, ReadClipboardText(), "The test history content did not reach the current clipboard.");

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
            () => window.Find<Button>(By.Name("Clipboard history"), 15_000),
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

    private async Task<ClipboardHistoryItem[]> WaitForHistoryAsync(Func<IReadOnlyList<ClipboardHistoryItem>, bool> matches, string message)
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

            await Task.Delay(100);
        }

        Assert.Fail($"{message} Last Windows history item count: {lastCount}; known test-owned IDs: {ownedHistoryItems.Count}.");
        return [];
    }

    private async Task DiscoverOwnedHistoryItemsAsync(IEnumerable<ClipboardHistoryItem> items)
    {
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
            history =>
            {
                foreach (var item in history.Where(item => ownedHistoryItems.ContainsKey(item.Id)))
                {
                    if (deletedIds.Add(item.Id))
                    {
                        Assert.IsTrue(
                            Target.Invoke(() => WinClipboard.DeleteItemFromHistory(item)),
                            "Windows could not delete a test-owned clipboard-history item during cleanup.");
                    }
                }

                return history.All(item => !ownedHistoryItems.ContainsKey(item.Id));
            },
            "Test-owned clipboard-history IDs remained after cleanup.");
        Assert.IsTrue(
            originalHistoryIds.IsSubsetOf(remaining.Select(item => item.Id)),
            "An unrelated original Windows history item was lost during the scenario.");
    }

    private sealed record HistoryFixture(string Id, string Content);

    private sealed record ClipboardHistoryRegistrySnapshot(bool KeyExisted, bool ValueExisted, object? Value, RegistryValueKind Kind, bool HistoryEnabled)
    {
        internal static ClipboardHistoryRegistrySnapshot Capture()
        {
            using var key = Registry.CurrentUser.OpenSubKey(ClipboardRegistryPath);
            var valueExisted = key?.GetValueNames().Contains(ClipboardRegistryValue, StringComparer.OrdinalIgnoreCase) == true;
            return new ClipboardHistoryRegistrySnapshot(
                key is not null,
                valueExisted,
                valueExisted ? key!.GetValue(ClipboardRegistryValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null,
                valueExisted ? key!.GetValueKind(ClipboardRegistryValue) : RegistryValueKind.Unknown,
                WinClipboard.IsHistoryEnabled());
        }

        internal void Restore()
        {
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
