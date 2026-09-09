// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests;

[TestClass]
[DoNotParallelize]
public sealed class PowerDisplayProfileKeyboardReorderingTests : UITestBase
{
    private const string ApplyButtonId = "ProfileApplyButton";
    private const string MoreButtonId = "ProfileMoreButton";
    private const string InitialOrder = "1,2,3,4";
    private const string ReorderedOrder = "1,3,2,4";
    private const string TerminatePowerDisplayEvent = @"Local\PowerToysPowerDisplay-TerminateEvent-7b9c2e1f-8a5d-4c3e-9f6b-2a1d8c5e3b7a";
    private static readonly int[] ProfileIds = [1, 2, 3, 4];
    private static readonly string[] ProcessNames = ["PowerToys.Settings", "PowerToys"];
    private static IDisposable? profilesSnapshot;
    private static IDisposable? moduleSettingsSnapshot;

    private static string ProfilesPath => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "PowerDisplay", "profiles.json");

    public PowerDisplayProfileKeyboardReorderingTests()
        : base(PowerToysModule.PowerToysSettings, size: WindowSize.Large, enableModules: ["PowerDisplay"])
    {
    }

    [ClassInitialize]
    public static void PreserveProfiles(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        StopPowerToysProcesses();
        AssertNoCrashMarkers();
        profilesSnapshot = SettingsConfigHelper.PreserveFile(ProfilesPath);
        try
        {
            moduleSettingsSnapshot = SettingsConfigHelper.PreserveModuleSettings("PowerDisplay");
        }
        catch
        {
            profilesSnapshot.Dispose();
            profilesSnapshot = null;
            throw;
        }
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreProfiles()
    {
        try
        {
            StopPowerToysProcesses();
            AssertNoCrashMarkers();
        }
        finally
        {
            try
            {
                var snapshot = profilesSnapshot;
                profilesSnapshot = null;
                snapshot?.Dispose();
            }
            finally
            {
                var snapshot = moduleSettingsSnapshot;
                moduleSettingsSnapshot = null;
                snapshot?.Dispose();
            }
        }
    }

    protected override void PrepareTestState()
    {
        AssertNoCrashMarkers();

        // This scenario needs the running module, but must not restore monitor hardware state.
        SettingsConfigHelper.UpdateModuleSettings(
            "PowerDisplay",
            """{"name":"PowerDisplay","version":"1","properties":{}}""",
            settings =>
            {
                if (settings["properties"] is not JsonObject properties)
                {
                    properties = new JsonObject();
                    settings["properties"] = properties;
                }

                properties["restore_settings_on_startup"] = false;
            });

        // Seed after the previous Settings/runner processes exit, so cleanup cannot overwrite it.
        // Stable ids and orders avoid a migration write. No test invokes the Apply button.
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilesPath)!);
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(new
        {
            profiles = ProfileIds.Select(id => new
            {
                id,
                name = ProfileName(id),
                order = id - 1,
                monitorSettings = new[] { new { monitorId = "powerdisplay-keyboard-test-monitor", brightness = 50 } },
                createdDate = timestamp,
                lastModified = timestamp,
            }),
            nextId = 5,
            lastUpdated = timestamp,
        }));
    }

    [TestCleanup]
    public async Task StopPowerDisplayAfterTestAsync()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        // Run before base cleanup, which kills the runner's process tree. The module's
        // cooperative exit hook removes discovery.lock even if DDC discovery is in progress.
        StopPowerToysProcesses();
        AssertNoCrashMarkers();
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("PowerDisplay")]
    [DataRow(Key.Up, ApplyButtonId, 3, DisplayName = "PowerDisplay.KeyboardReorder.Apply.Up")]
    [DataRow(Key.Left, ApplyButtonId, 3)]
    [DataRow(Key.Down, ApplyButtonId, 2)]
    [DataRow(Key.Right, ApplyButtonId, 2)]
    [DataRow(Key.Up, MoreButtonId, 3)]
    [DataRow(Key.Left, MoreButtonId, 3)]
    [DataRow(Key.Down, MoreButtonId, 2)]
    [DataRow(Key.Right, MoreButtonId, 2)]
    public void ReorderShortcut_MovesOnceAndPersistsAfterReload(Key direction, string buttonId, int profileId)
    {
        NavigateToProfiles();
        AssertStableOrder(InitialOrder);
        FocusProfileButton(profileId, buttonId);

        Step($"Sending Alt+Shift+{direction} from profile {profileId} {buttonId}");
        KeyboardHelper.SendKeys(Key.Alt, Key.Shift, direction);

        // Check both views after the asynchronous save/reload. Native reordering must not move
        // the collection a second time after the page handles the preview event.
        AssertStableOrder(ReorderedOrder);
        Assert.IsTrue(
            GetProfileButton(profileId, MoreButtonId).WaitForProperty("HasKeyboardFocus", "True", 5_000),
            $"Keyboard focus did not return to the moved profile {profileId}.");

        Step("Navigating away and reopening Power Display to reload the persisted order");
        Find<NavigationViewItem>(By.AccessibilityId("GeneralNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(Session.WaitFor(() => !Session.Has(By.AccessibilityId("ProfilesList"), 100)), "The Power Display page did not unload.");
        NavigateToProfiles();
        AssertStableOrder(ReorderedOrder);
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("PowerDisplay")]
    [DataRow(Key.Up, ApplyButtonId, 1)]
    [DataRow(Key.Left, MoreButtonId, 1)]
    [DataRow(Key.Down, ApplyButtonId, 4)]
    [DataRow(Key.Right, MoreButtonId, 4)]
    public void ReorderShortcut_AtBoundaryDoesNotWrite(Key direction, string buttonId, int profileId)
    {
        NavigateToProfiles();
        AssertStableOrder(InitialOrder);
        var originalFile = File.ReadAllBytes(ProfilesPath);
        FocusProfileButton(profileId, buttonId);

        Step($"Sending boundary Alt+Shift+{direction} from profile {profileId}");
        KeyboardHelper.SendKeys(Key.Alt, Key.Shift, direction);

        AssertStableOrder(InitialOrder);
        CollectionAssert.AreEqual(originalFile, File.ReadAllBytes(ProfilesPath), "A boundary shortcut rewrote the profiles file.");
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("PowerDisplay")]
    public void OtherDirectionKeys_DoNotReorderOrWrite()
    {
        NavigateToProfiles();
        AssertStableOrder(InitialOrder);
        var originalFile = File.ReadAllBytes(ProfilesPath);

        foreach (var direction in new[] { Key.Up, Key.Down, Key.Left, Key.Right })
        {
            foreach (var keys in new[] { new[] { direction }, new[] { Key.Ctrl, Key.Alt, Key.Shift, direction } })
            {
                FocusProfileButton(2, ApplyButtonId);
                Step($"Sending {string.Join("+", keys)} from profile 2");
                KeyboardHelper.SendKeys(keys);

                AssertStableOrder(InitialOrder);
                CollectionAssert.AreEqual(originalFile, File.ReadAllBytes(ProfilesPath), $"{string.Join("+", keys)} rewrote the profiles file.");
            }
        }
    }

    private static string ProfileName(int id) => $"Keyboard reorder fixture {id}";

    private static void StopPowerToysProcesses()
    {
        // Signal the module's existing terminate event, allowing its ProcessExit hook to run.
        // Killing its process tree during discovery would leave a false crash quarantine.
        var moduleProcesses = Process.GetProcessesByName("PowerToys.PowerDisplay");
        try
        {
            if (moduleProcesses.Length > 0)
            {
                // The process may finish while waiting for its event; exit is the final signal.
                NamedEventHelper.WaitAndSignal(TerminatePowerDisplayEvent, timeoutMS: 10_000);
                foreach (var process in moduleProcesses)
                {
                    if (!process.WaitForExit(10_000))
                    {
                        throw new InvalidOperationException("Power Display did not exit cooperatively; refusing to kill it during monitor discovery.");
                    }
                }
            }
        }
        finally
        {
            foreach (var process in moduleProcesses)
            {
                process.Dispose();
            }
        }

        foreach (var processName in ProcessNames)
        {
            if (!WindowControl.TryKillProcessTreeByNameAndWait(processName))
            {
                throw new InvalidOperationException($"Cannot prepare or restore profiles while {processName} is still running.");
            }
        }
    }

    private static void AssertNoCrashMarkers()
    {
        foreach (var name in new[] { "discovery.lock", "crash_detected.flag" })
        {
            var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "PowerDisplay", name);
            Assert.IsFalse(File.Exists(path), $"Power Display crash evidence exists at {path}. Preserving it; the keyboard test requires an unquarantined module.");
        }
    }

    private void NavigateToProfiles()
    {
        Step("Opening Power Display profiles");
        Assert.IsTrue(Session.IsElevated == false, "Keyboard reordering must be tested with a non-elevated Settings process, where native reordering is enabled.");
        if (!Session.Has(By.AccessibilityId("PowerDisplayNavItem"), 500))
        {
            Find<NavigationViewItem>(By.AccessibilityId("InputOutputNavItem")).Click(msPostAction: 0);
        }

        Find<NavigationViewItem>(By.AccessibilityId("PowerDisplayNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(Session.WaitForElement(By.AccessibilityId("ProfilesList")), "Power Display profiles did not load.");
        Assert.IsTrue(
            Find<Element>(By.AccessibilityId("ProfilesList")).WaitForProperty("IsEnabled", "True", 10_000),
            "Power Display profiles did not finish loading.");

        Step("Scrolling the page until all four profile buttons are visible");
        var viewport = WaitHelper.WaitForStable(
            observe: () => AreAllProfilesVisible(Session.FindAll<Button>(By.AccessibilityId(ApplyButtonId), 1_000)),
            isMatch: visible => visible,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100,
            recover: _ => Find<Element>(By.AccessibilityId("PageScrollViewer"), 1_000).Scroll(ScrollDirection.Down));
        Assert.IsTrue(viewport.Succeeded, "The page did not scroll all four profile buttons into view.");
    }

    private Button GetProfileButton(int profileId, string buttonId)
    {
        // HelpText identifies the fixture without relying on the button's translated caption.
        var buttons = Session.FindAll<Button>(By.AccessibilityId(buttonId))
            .Where(button => button.HelpText.Contains(ProfileName(profileId), StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, buttons.Length, $"Expected one {buttonId} for profile {profileId}.");
        return buttons[0];
    }

    private void FocusProfileButton(int profileId, string buttonId)
    {
        Step($"Focusing profile {profileId} {buttonId}");
        Session.EnsureForeground();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), requiredConsecutiveMatches: 2),
            $"Settings did not obtain keyboard input. Foreground: {WindowControl.GetForegroundWindowInfo()}");
        var button = GetProfileButton(profileId, buttonId);
        button.Focus();
        Assert.IsTrue(button.WaitForProperty("HasKeyboardFocus", "True"), $"{buttonId} for profile {profileId} did not receive keyboard focus.");
    }

    private void AssertStableOrder(string expectedOrder)
    {
        Step($"Waiting for enabled profiles and stable UI/persisted order {expectedOrder}");
        var result = WaitHelper.WaitForStable(
            observe: ObserveOrder,
            isMatch: state => state is not null && state.Enabled && state.AllProfilesVisible && state.UiOrder == expectedOrder && state.PersistedOrder == expectedOrder,
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is IOException || (ex is AssertFailedException && ex.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Succeeded, $"Expected order {expectedOrder} after save/reload. Last state: {result.LastObservation}; error: {result.LastException}");
    }

    private OrderObservation ObserveOrder()
    {
        var buttons = Session.FindAll<Button>(By.AccessibilityId(ApplyButtonId), 1_000);
        var visibleOrder = buttons.OrderBy(button => button.Y).Select(button =>
        {
            var helpText = button.HelpText;
            return ProfileIds.SingleOrDefault(id => helpText.Contains(ProfileName(id), StringComparison.Ordinal));
        });
        using var document = JsonDocument.Parse(File.ReadAllText(ProfilesPath));
        var persistedOrder = document.RootElement.GetProperty("profiles").EnumerateArray()
            .OrderBy(profile => profile.GetProperty("order").GetInt32())
            .Select(profile => profile.GetProperty("id").GetInt32());
        var enabled = string.Equals(Find<Element>(By.AccessibilityId("ProfilesList"), 1_000).GetProperty("IsEnabled"), "True", StringComparison.OrdinalIgnoreCase);
        var allProfilesVisible = AreAllProfilesVisible(buttons);
        return new OrderObservation(string.Join(",", visibleOrder), string.Join(",", persistedOrder), enabled, allProfilesVisible);
    }

    private static bool AreAllProfilesVisible(IReadOnlyCollection<Button> buttons)
    {
        return buttons.Count == ProfileIds.Length && buttons.All(button =>
            button.Width > 0 && button.Height > 0 && string.Equals(button.GetProperty("IsOffscreen"), "False", StringComparison.OrdinalIgnoreCase));
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    private sealed record OrderObservation(string UiOrder, string PersistedOrder, bool Enabled, bool AllProfilesVisible);
}
