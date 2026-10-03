// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

[TestClass]
public class MouseButtonLockSettingsTests : UITestBase
{
    private const string ModuleName = "MouseButtonLock";
    private const string GroupId = "MouseUtils_MouseButtonLockTestId";
    private const string ModuleToggleId = "MouseUtils_MouseButtonLockToggleId";
    private const string OptionsExpanderId = "MouseUtils_MouseButtonLockOptionsId";
    private const string LmbLockId = "MouseUtils_MouseButtonLockLmbLockId";
    private const string RmbLockId = "MouseUtils_MouseButtonLockRmbLockId";
    private const string MmbLockId = "MouseUtils_MouseButtonLockMmbLockId";
    private const string HoldDurationId = "MouseUtils_MouseButtonLockHoldDurationId";
    private const string MoveCancelPixelsId = "MouseUtils_MouseButtonLockMoveCancelPixelsId";

    // A short configured hold duration keeps the behavioral lock tests fast while still exercising
    // the real engine threshold (native minimum is 200 ms; see MouseButtonLockCore.h).
    private const int FastHoldDurationMs = 300;

    // Slack added on top of the configured hold duration so scheduler jitter never makes an
    // "over the threshold" hold look like it landed under it.
    private const int HoldSlackMs = 300;

    // Comfortably under the configured hold duration so an "under the threshold" release can never
    // flake into locking.
    private const int UnderThresholdSlackMs = 150;

    // Settings applies module enable/disable through the runner asynchronously, so the hook can
    // still be absent (or still present) for a moment after the toggle reports its new state.
    private const int HookSettleTimeoutMs = 10_000;

    // How long a released button must keep reading as down before it counts as locked.
    private const int LockObservationMs = 1_000;

    // The tests drive the left and middle buttons. A right-button release that does not lock opens
    // the desktop context menu, which would leak into later steps.
    private static readonly LockButton Left = new("left", 0x01, MouseHelper.LeftDown, MouseHelper.LeftUp, MouseHelper.LeftClick);
    private static readonly LockButton Right = new("right", 0x02, MouseHelper.RightDown, MouseHelper.RightUp, MouseHelper.RightClick);
    private static readonly LockButton Middle = new("middle", 0x04, MouseHelper.MiddleDown, MouseHelper.MiddleUp, MouseHelper.MiddleClick);

    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);

    static MouseButtonLockSettingsTests()
    {
    }

    public MouseButtonLockSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    protected override void PrepareTestState()
    {
        var settings = TestContext.TestName switch
        {
            nameof(SectionNavigationAndModuleLifecycleAreAvailable) =>
                CreateSettings(lmbLock: false, rmbLock: false, mmbLock: true, holdDurationMs: FastHoldDurationMs, moveCancelPixels: 5),
            nameof(HeldButtonLocksPastThresholdAndReleasesOnTap) =>
                CreateSettings(lmbLock: true, rmbLock: false, mmbLock: false, holdDurationMs: FastHoldDurationMs, moveCancelPixels: 5),
            nameof(ButtonLockCheckboxesPersistIndependently) =>
                CreateSettings(lmbLock: false, rmbLock: false, mmbLock: false, holdDurationMs: 1200, moveCancelPixels: 5),
            _ =>
                CreateSettings(lmbLock: false, rmbLock: true, mmbLock: false, holdDurationMs: 1200, moveCancelPixels: 5),
        };
        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, settings);
    }

    [TestCleanup]
    public async Task ReleaseLocksAndDisableModule()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        try
        {
            // A failed assertion can leave a lock engaged. Release it with the module's own tap
            // gesture, but only for a button that still reads as down, and with Settings minimized so
            // the tap cannot click a control.
            var stuck = new[] { Left, Right, Middle }.Where(button => IsButtonDown(button.VirtualKey)).ToArray();
            if (stuck.Length > 0)
            {
                WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
                foreach (var button in stuck)
                {
                    button.Tap();
                }

                Session.EnsureForeground();
            }
        }
        catch
        {
            // Best effort; the desktop may already be in a torn-down state if the test failed early.
        }

        try
        {
            if (Session.Has(By.AccessibilityId(ModuleToggleId), 500))
            {
                var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId(ModuleToggleId), 500);
                if (toggle.IsOn)
                {
                    toggle.Toggle(false);
                    toggle.WaitForProperty("ToggleState", "Off", 5_000);
                }
            }
        }
        catch
        {
            // The base cleanup will stop the test-owned Runner if Settings is no longer reachable.
        }
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("MouseButtonLock")]
    public void SectionNavigationAndModuleLifecycleAreAvailable()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);

        var group = Session.Find<Element>(By.AccessibilityId(GroupId), 10_000);
        Assert.IsTrue(group.Displayed, "Mouse Button Lock settings group was not visible.");

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, false);
        var expander = Session.Find<Element>(By.AccessibilityId(OptionsExpanderId), 5_000);
        Assert.IsFalse(expander.IsEnabled, "Buttons and behavior options should be disabled with the module.");

        // The module runs inside the runner with no worker process, window, or named event to probe,
        // so its disabled/enabled effect is asserted the way it is externally observable: whether
        // holding a configured button past the hold duration actually locks it.
        AssertLockOnceSettled(Middle, expectLock: false);

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);
        expander = Session.Find<Element>(By.AccessibilityId(OptionsExpanderId), 5_000);
        Assert.IsTrue(expander.IsEnabled, "Buttons and behavior options should be enabled with the module.");

        AssertLockOnceSettled(Middle, expectLock: true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("MouseButtonLock")]
    public void ButtonLockCheckboxesPersistIndependently()
    {
        OpenOptions();

        AssertCheckBoxState(LmbLockId, expectedChecked: false);
        AssertCheckBoxState(RmbLockId, expectedChecked: false);
        AssertCheckBoxState(MmbLockId, expectedChecked: false);

        SetCheckBox(LmbLockId, check: true);
        AssertPersistedBool("lmb_lock_enabled", true);
        AssertPersistedBool("rmb_lock_enabled", false);
        AssertPersistedBool("mmb_lock_enabled", false);

        SetCheckBox(RmbLockId, check: true);
        AssertPersistedBool("lmb_lock_enabled", true);
        AssertPersistedBool("rmb_lock_enabled", true);
        AssertPersistedBool("mmb_lock_enabled", false);

        SetCheckBox(MmbLockId, check: true);
        AssertPersistedBool("lmb_lock_enabled", true);
        AssertPersistedBool("rmb_lock_enabled", true);
        AssertPersistedBool("mmb_lock_enabled", true);

        SetCheckBox(LmbLockId, check: false);
        AssertPersistedBool("lmb_lock_enabled", false);
        AssertPersistedBool("rmb_lock_enabled", true);
        AssertPersistedBool("mmb_lock_enabled", true);

        RestartScope();
        OpenOptions();
        AssertCheckBoxState(LmbLockId, expectedChecked: false);
        AssertCheckBoxState(RmbLockId, expectedChecked: true);
        AssertCheckBoxState(MmbLockId, expectedChecked: true);
        AssertPersistedBool("lmb_lock_enabled", false);
        AssertPersistedBool("rmb_lock_enabled", true);
        AssertPersistedBool("mmb_lock_enabled", true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("MouseButtonLock")]
    public void HoldDurationAndMoveCancelPixelsPersistAtBoundaries()
    {
        OpenOptions();

        Session.Find<Slider>(By.AccessibilityId(HoldDurationId), 5_000).SetValue(200);
        AssertPersistedInt("hold_duration_ms", 200);
        Session.Find<Slider>(By.AccessibilityId(HoldDurationId), 5_000).SetValue(2200);
        AssertPersistedInt("hold_duration_ms", 2200);

        Session.Find<NumberBox>(By.AccessibilityId(MoveCancelPixelsId), 5_000).SetValue(0);
        AssertPersistedInt("move_cancel_pixels", 0);
        Session.Find<NumberBox>(By.AccessibilityId(MoveCancelPixelsId), 5_000).SetValue(100);
        AssertPersistedInt("move_cancel_pixels", 100);

        RestartScope();
        OpenOptions();
        Assert.AreEqual(
            2200d,
            Session.Find<Slider>(By.AccessibilityId(HoldDurationId), 5_000).Value,
            0.01,
            "Hold duration slider did not reflect its persisted maximum value after restart.");
        AssertPersistedInt("hold_duration_ms", 2200);
        AssertPersistedInt("move_cancel_pixels", 100);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("MouseButtonLock")]
    public void HeldButtonLocksPastThresholdAndReleasesOnTap()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);

        // Holding past the configured hold duration locks the left button, and a same-button tap (the
        // engine's release gesture, see MouseButtonLockCore.h) fully releases it. Waiting for this
        // first lock also confirms the hook is live before the negative checks below.
        AssertLockOnceSettled(Left, expectLock: true);

        // Releasing before the configured hold duration elapses is an ordinary click.
        Assert.IsFalse(
            HoldAndRelease(Left, FastHoldDurationMs - UnderThresholdSlackMs),
            "The left button locked after a release under the configured hold duration.");

        // The middle button is disabled in the seeded baseline, so a hold past the duration never locks it.
        Assert.IsFalse(
            HoldAndRelease(Middle, FastHoldDurationMs + HoldSlackMs),
            "The middle button locked even though its lock is disabled.");
    }

    private void OpenOptions()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);

        if (!Session.Has(By.AccessibilityId(LmbLockId), 500))
        {
            Session.EnsureForeground();
            Session.Find<Element>(By.AccessibilityId(OptionsExpanderId), 5_000).MouseClick(msPostAction: 500);
        }

        Assert.IsTrue(Session.Has(By.AccessibilityId(LmbLockId), 5_000), "LMB lock checkbox was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(RmbLockId), 5_000), "RMB lock checkbox was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(MmbLockId), 5_000), "MMB lock checkbox was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(HoldDurationId), 5_000), "Hold duration slider was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(MoveCancelPixelsId), 5_000), "Move-cancel pixels control was not available.");
    }

    private void SetCheckBox(string accessibilityId, bool check)
    {
        var checkBox = Session.Find<CheckBox>(By.AccessibilityId(accessibilityId), 5_000);
        checkBox.SetCheck(check);
        Assert.AreEqual(check, checkBox.IsChecked, $"{accessibilityId} did not reach the expected checked state.");
    }

    private void AssertCheckBoxState(string accessibilityId, bool expectedChecked)
    {
        var checkBox = Session.Find<CheckBox>(By.AccessibilityId(accessibilityId), 5_000);
        Assert.AreEqual(expectedChecked, checkBox.IsChecked, $"{accessibilityId} did not have the expected persisted state.");
    }

    /// <summary>
    /// Repeats a hold of <paramref name="button"/> past the configured duration until its lock state
    /// matches <paramref name="expectLock"/>, for up to <see cref="HookSettleTimeoutMs"/>. This absorbs
    /// the delay between the Settings toggle and the runner installing or removing the hook.
    /// </summary>
    private void AssertLockOnceSettled(LockButton button, bool expectLock)
    {
        var stopwatch = Stopwatch.StartNew();
        var locked = HoldAndRelease(button, FastHoldDurationMs + HoldSlackMs);
        while (locked != expectLock && stopwatch.ElapsedMilliseconds < HookSettleTimeoutMs)
        {
            Thread.Sleep(250);
            locked = HoldAndRelease(button, FastHoldDurationMs + HoldSlackMs);
        }

        Assert.AreEqual(
            expectLock,
            locked,
            $"Holding the {button.Name} button past the hold duration was expected to {(expectLock ? "lock it" : "leave it unlocked")}.");
    }

    /// <summary>
    /// Presses <paramref name="button"/> at the screen center, holds it for <paramref name="holdMs"/>
    /// without moving the cursor, releases it, and returns whether it locked. A locked button keeps
    /// reading as down in <c>GetAsyncKeyState</c> because the hook swallows the release before it
    /// reaches the system key state. Any lock is released again with the engine's same-button tap,
    /// and that release is asserted, so no attempt leaves a button held. Settings is minimized
    /// during the gesture so the press cannot land on one of its controls.
    /// </summary>
    private bool HoldAndRelease(LockButton button, int holdMs)
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        try
        {
            var (centerX, centerY) = WindowHelper.GetScreenCenter();
            MouseHelper.MoveTo(centerX, centerY);

            button.Down();
            Thread.Sleep(holdMs);
            button.Up();

            var locked = !WaitForButtonReleased(button, LockObservationMs);
            if (locked)
            {
                button.Tap();
                Assert.IsTrue(
                    WaitForButtonReleased(button, 2_000),
                    $"A {button.Name} tap did not release the lock.");
            }

            return locked;
        }
        finally
        {
            Session.EnsureForeground();
        }
    }

    private static bool WaitForButtonReleased(LockButton button, int timeoutMs) =>
        WaitHelper.WaitForStable(
            () => IsButtonDown(button.VirtualKey),
            down => !down,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 20).Succeeded;

    private static bool IsButtonDown(int virtualKeyCode) => (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;

    private static void AssertPersistedBool(string propertyName, bool expected)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadPersistedBool(propertyName),
            actual => actual == expected,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"{propertyName} did not persist as {expected}. Last observed value: {result.LastObservation}.");
    }

    private static void AssertPersistedInt(string propertyName, int expected)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadPersistedInt(propertyName),
            actual => actual == expected,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"{propertyName} did not persist as {expected}. Last observed value: {result.LastObservation}.");
    }

    private static bool? ReadPersistedBool(string propertyName)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        return document.RootElement.GetProperty("properties").GetProperty(propertyName).GetProperty("value").GetBoolean();
    }

    private static int? ReadPersistedInt(string propertyName)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        return document.RootElement.GetProperty("properties").GetProperty(propertyName).GetProperty("value").GetInt32();
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        ModuleName,
        "settings.json");

    private static string CreateSettings(bool lmbLock, bool rmbLock, bool mmbLock, int holdDurationMs, int moveCancelPixels) => $$"""
        {
          "name": "MouseButtonLock",
          "version": "1.0",
          "properties": {
            "lmb_lock_enabled": { "value": {{lmbLock.ToString().ToLowerInvariant()}} },
            "rmb_lock_enabled": { "value": {{rmbLock.ToString().ToLowerInvariant()}} },
            "mmb_lock_enabled": { "value": {{mmbLock.ToString().ToLowerInvariant()}} },
            "hold_duration_ms": { "value": {{holdDurationMs}} },
            "move_cancel_pixels": { "value": {{moveCancelPixels}} }
          }
        }
        """;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    private sealed record LockButton(string Name, int VirtualKey, Action Down, Action Up, Action Tap);
}
