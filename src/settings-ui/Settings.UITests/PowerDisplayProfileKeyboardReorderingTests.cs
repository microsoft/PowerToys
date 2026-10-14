// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private const string FixtureNamePrefix = "Keyboard reorder fixture ";
    private const int VirtualizedProfileCount = 20;
    private const string InitialOrder = "1,2,3,4";
    private const string ReorderedOrder = "1,3,2,4";
    private const string TerminatePowerDisplayEvent = @"Local\PowerToysPowerDisplay-TerminateEvent-7b9c2e1f-8a5d-4c3e-9f6b-2a1d8c5e3b7a";
    private static readonly int[] ProfileIds = [1, 2, 3, 4];
    private static readonly string[] ProcessNames = ["PowerToys.Settings", "PowerToys"];
    private static IDisposable? profilesSnapshot;
    private static IDisposable? moduleSettingsSnapshot;
    private static IDisposable? lightSwitchSettingsSnapshot;

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
            // Module startup reconciles Light Switch references against the fixture profiles.
            lightSwitchSettingsSnapshot = SettingsConfigHelper.PreserveModuleSettings("LightSwitch");
        }
        catch
        {
            RestoreSettingsSnapshots();
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
            RestoreSettingsSnapshots();
        }
    }

    private static void RestoreSettingsSnapshots()
    {
        try
        {
            var snapshot = profilesSnapshot;
            profilesSnapshot = null;
            snapshot?.Dispose();
        }
        finally
        {
            try
            {
                var snapshot = moduleSettingsSnapshot;
                moduleSettingsSnapshot = null;
                snapshot?.Dispose();
            }
            finally
            {
                var snapshot = lightSwitchSettingsSnapshot;
                lightSwitchSettingsSnapshot = null;
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
        // Stable ids avoid a migration write. Array positions define the initial display order.
        // No test invokes the Apply button.
        var profileCount = TestContext.TestName == nameof(ReorderDrag_AcrossViewportPersistsOrder)
            ? VirtualizedProfileCount
            : ProfileIds.Length;
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilesPath)!);
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(new
        {
            profiles = Enumerable.Range(1, profileCount).Select(id => new
            {
                id,
                name = ProfileName(id),
                monitorSettings = new[] { new { monitorId = "powerdisplay-keyboard-test-monitor", brightness = 50 } },
                createdDate = timestamp,
                lastModified = timestamp,
            }),
            nextId = profileCount + 1,
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

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("PowerDisplay")]
    public void ReorderDrag_AcrossViewportPersistsOrder()
    {
        using var dpiScope = new PhysicalCoordinateScope();
        NavigateToProfiles(useVirtualizedList: true);
        Assert.AreEqual(string.Join(",", Enumerable.Range(1, VirtualizedProfileCount)), ReadPersistedOrder());
        Session.EnsureForeground();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), requiredConsecutiveMatches: 2),
            $"Settings did not obtain mouse input. Foreground: {WindowControl.GetForegroundWindowInfo()}");

        var page = Find<Element>(By.AccessibilityId("PageScrollViewer"));
        var list = Find<Element>(By.AccessibilityId("ProfilesList"));
        var listRight = list.X + list.Width;
        var gutterX = listRight + ((page.X + page.Width - listRight) / 2);
        var gutterY = page.Y + (page.Height / 2);
        Assert.IsTrue(gutterX > listRight && gutterX < page.X + page.Width, "The page has no outer scrolling gutter to the right of the profile list.");

        Step("Measuring complete second and third profile rows before the drag");
        list.ScrollToEdge(toBottom: false);
        Element? firstRow = null;
        Element? secondRow = null;
        Element? thirdRow = null;
        var topViewport = WaitHelper.WaitForStable(
            observe: () =>
            {
                firstRow = FindProfileRow(1);
                secondRow = FindProfileRow(2);
                thirdRow = FindProfileRow(3);
                return firstRow is { Height: > 0 } && secondRow is { Height: > 0 } && thirdRow is { Height: > 0 } &&
                    Math.Abs(secondRow.Height - thirdRow.Height) <= 1 && firstRow.Y > page.Y &&
                    IsProfileButtonVisible(1, MoreButtonId);
            },
            isMatch: complete => complete,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100,
            recover: _ => ScrollOuterPageDown(gutterX, gutterY));
        Assert.IsTrue(topViewport.Succeeded, $"Could not measure complete second and third rows. Heights: {firstRow?.Height}, {secondRow?.Height}, {thirdRow?.Height}.");

        // The expander's negative content margin slightly clips row 1 even at the list's top.
        // Preserve that observed baseline separately from the full height of the interior rows.
        var firstRowBaselineHeight = firstRow!.Height;
        var fullRowHeight = secondRow!.Height;
        Step($"Measured first-row baseline height {firstRowBaselineHeight}; complete row height {fullRowHeight}");

        Step("Locating the bottom edge of the profile viewport using the complete last row");
        Find<Element>(By.AccessibilityId("ProfilesList")).ScrollToEdge(toBottom: true);
        Element? lastRow = null;
        var bottomViewport = WaitHelper.WaitForStable(
            observe: () =>
            {
                lastRow = FindProfileRow(VirtualizedProfileCount);
                return lastRow is not null && Math.Abs(lastRow.Height - fullRowHeight) <= 1 &&
                    IsProfileButtonVisible(VirtualizedProfileCount, MoreButtonId);
            },
            isMatch: complete => complete,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100,
            recover: _ => ScrollOuterPageDown(gutterX, gutterY));
        Assert.IsTrue(bottomViewport.Succeeded, $"The last row did not become complete. Expected height: {fullRowHeight}; actual: {lastRow?.Height}.");
        var bottomAnchorY = lastRow!.Y + lastRow.Height - Math.Max(4, fullRowHeight / 8);
        var calibratedLastRowBounds = $"({lastRow.X}, {lastRow.Y}, {lastRow.Width}, {lastRow.Height})";

        Find<Element>(By.AccessibilityId("ProfilesList")).ScrollToEdge(toBottom: false);
        Assert.IsTrue(
            Session.WaitFor(() =>
            {
                firstRow = FindProfileRow(1);
                return firstRow is not null && Math.Abs(firstRow.Height - firstRowBaselineHeight) <= 1 && firstRow.Y > page.Y &&
                    IsProfileButtonVisible(1, MoreButtonId) &&
                    !IsProfileButtonVisible(VirtualizedProfileCount, MoreButtonId);
            }),
            "The drag must start with the first row at its observed baseline and the last row outside the viewport.");

        // From this point on, scrolling comes only from ListView's native drag edge scrolling.
        var applyButton = FindVisibleProfileButton(1, ApplyButtonId);
        Assert.IsNotNull(applyButton, "The first profile's Apply button is not visible for locating the header drag target.");
        var headerCandidates = Session.FindAll<Element>(By.Name(ProfileName(1)))
            .Where(element => string.Equals(element.ControlType, "Text", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(element.ClassName, "TextBlock", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var candidate in headerCandidates)
        {
            TestContext.WriteLine($"Header drag candidate Name='{candidate.Name}', bounds=({candidate.X}, {candidate.Y}, {candidate.Width}, {candidate.Height})");
        }

        var headers = headerCandidates.Where(element => element.Name == firstRow!.Name && element.Width > 0 && element.Height > 0 &&
                element.X >= firstRow.X && element.Y >= firstRow.Y &&
                element.X + element.Width <= firstRow.X + firstRow.Width && element.Y + element.Height <= firstRow.Y + firstRow.Height &&
                element.X + element.Width < applyButton.X && string.Equals(element.GetProperty("IsOffscreen"), "False", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.AreEqual(1, headers.Length, "The drag setup requires one visible profile header TextBlock inside row 1 and to the left of Apply.");
        var header = headers[0];
        var startX = header.X + (header.Width / 2);
        var startY = header.Y + (header.Height / 2);
        Assert.IsTrue(startX < applyButton.X && bottomAnchorY > startY, "The drag must begin to the left of Apply and move down toward the viewport edge.");
        list = Find<Element>(By.AccessibilityId("ProfilesList"));
        TestContext.WriteLine($"Profile list bounds before drag=({list.X}, {list.Y}, {list.Width}, {list.Height}); bottom anchor={bottomAnchorY}; calibrated row 20 bounds={calibratedLastRowBounds}");
        TestContext.WriteLine($"Profile list ScrollVerticalPercent before drag: {list.GetProperty("ScrollVerticalPercent")}");
        AssertSettingsForeground("before the drag");
        Step($"Dragging profile 1 from ({startX}, {startY}) to the lower viewport edge {bottomAnchorY}; full row height {fullRowHeight}");
        try
        {
            AssertMovePhysicalPointer(startX, startY);
            Thread.Sleep(100);
            MouseHelper.LeftDown();
            Thread.Sleep(100);
            Assert.IsTrue(KeyboardHelper.IsKeyDown((Key)0x01), "The left mouse button was not held after mouse-down; the drag input scenario is invalid.");
            for (var step = 1; step <= 12; step++)
            {
                AssertMovePhysicalPointer(startX, startY + (((bottomAnchorY - startY) * step) / 12));
                Thread.Sleep(20);
            }

            var heldScreenshot = Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"powerdisplay-drag-held-{Guid.NewGuid():N}.png");
            if (ScreenCapture.TryCaptureDesktop(heldScreenshot))
            {
                TestContext.AddResultFile(heldScreenshot);
            }
            else
            {
                TestContext.WriteLine("Could not capture the desktop while the drag button was held.");
            }

            TestContext.WriteLine($"Profile list ScrollVerticalPercent while holding drag: {list.GetProperty("ScrollVerticalPercent")}");

            var jiggle = false;
            var reachedLastRow = WaitHelper.WaitForStable(
                observe: () =>
                {
                    AssertSettingsForeground("while dragging");
                    Assert.IsTrue(KeyboardHelper.IsKeyDown((Key)0x01), "The left mouse button was released during the gesture; the drag input scenario is invalid.");
                    lastRow = FindProfileRow(VirtualizedProfileCount);

                    // WinUI scales items during a drag, so their unscaled setup height no longer
                    // determines readiness. Require a visible row and its visible More button.
                    return lastRow is { Width: > 0, Height: > 0 } &&
                        string.Equals(lastRow.GetProperty("IsOffscreen"), "False", StringComparison.OrdinalIgnoreCase) &&
                        IsProfileButtonVisible(VirtualizedProfileCount, MoreButtonId);
                },
                isMatch: complete => complete,
                timeoutMS: 20_000,
                pollIntervalMS: 100,
                recover: _ =>
                {
                    jiggle = !jiggle;
                    AssertMovePhysicalPointer(startX + (jiggle ? 1 : 0), bottomAnchorY);
                });
            TestContext.WriteLine($"Profile list ScrollVerticalPercent at end of drag hold: {list.GetProperty("ScrollVerticalPercent")}");
            TestContext.WriteLine(lastRow is null
                ? "Last profile at end of drag hold: no ListItem found."
                : $"Last profile at end of drag hold: Name='{lastRow.Name}', bounds=({lastRow.X}, {lastRow.Y}, {lastRow.Width}, {lastRow.Height}), IsOffscreen={lastRow.GetProperty("IsOffscreen")}");
            var heldEndScreenshot = Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"powerdisplay-drag-held-end-{Guid.NewGuid():N}.png");
            if (ScreenCapture.TryCaptureDesktop(heldEndScreenshot))
            {
                TestContext.AddResultFile(heldEndScreenshot);
            }
            else
            {
                TestContext.WriteLine("Could not capture the desktop at the end of the drag hold.");
            }

            Assert.IsTrue(reachedLastRow.Succeeded, "Holding the drag at the viewport edge did not reveal the complete last profile.");
            AssertMovePhysicalPointer(startX, lastRow!.Y + ((lastRow.Height * 3) / 4));
            Thread.Sleep(300);
        }
        finally
        {
            try
            {
                AssertSettingsForeground("before releasing the drag");
            }
            finally
            {
                MouseHelper.LeftUp();
            }
        }

        AssertStablePersistedOrder(string.Join(",", Enumerable.Range(2, 19).Append(1)));
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("PowerDisplay")]
    public void ProfileListItems_ExposeDisplayNamesToAutomation()
    {
        NavigateToProfiles();
        AssertStableOrder(InitialOrder);

        var expectedNames = ProfileIds.Select(id => GetProfileButton(id, ApplyButtonId).HelpText).ToArray();
        var itemNames = Session.FindAll<Element>(By.Name(FixtureNamePrefix))
            .Where(element => string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Name)
            .ToArray();
        CollectionAssert.AreEquivalent(expectedNames, itemNames, "Every profile ListItem must expose its DisplayName as its automation Name.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out PhysicalPoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);

    private sealed class PhysicalCoordinateScope : IDisposable
    {
        private readonly IntPtr previousContext;

        public PhysicalCoordinateScope()
        {
            // UIA reports physical coordinates. Keep all native input in the same coordinate
            // system for this synchronous test, without changing the test host's global DPI mode.
            previousContext = SetThreadDpiAwarenessContext(new IntPtr(-4));
            var error = Marshal.GetLastWin32Error();
            Assert.AreNotEqual(IntPtr.Zero, previousContext, $"Could not enter per-monitor-aware V2 input scope; Win32 error {error}.");
        }

        public void Dispose()
        {
            var restored = SetThreadDpiAwarenessContext(previousContext);
            var error = Marshal.GetLastWin32Error();
            Assert.AreNotEqual(IntPtr.Zero, restored, $"Could not restore the original thread DPI context; Win32 error {error}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PhysicalPoint
    {
        public int X;
        public int Y;
    }

    // MOUSEINPUT is the largest INPUT union member. Keep native pointer alignment on both
    // structures: on x64/ARM64 Mouse starts at offset 8 and NativeInput occupies 40 bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public MouseInputData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    private void AssertMovePhysicalPointer(int x, int y)
    {
        // SM_XVIRTUALSCREEN through SM_CYVIRTUALSCREEN are read in the test's PMv2 scope.
        var left = GetSystemMetrics(76);
        var top = GetSystemMetrics(77);
        var width = GetSystemMetrics(78);
        var height = GetSystemMetrics(79);
        var relativeX = (long)x - left;
        var relativeY = (long)y - top;
        Assert.IsTrue(width > 0 && height > 0, "The virtual desktop has no valid physical dimensions.");
        Assert.IsTrue(relativeX >= 0 && relativeX < width && relativeY >= 0 && relativeY < height, "The requested pointer position is outside the virtual desktop.");
        var absoluteX = ((relativeX * 65536L) + 32768L) / width;
        var absoluteY = ((relativeY * 65536L) + 32768L) / height;
        Assert.IsTrue(absoluteX is >= 0 and <= 65535 && absoluteY is >= 0 and <= 65535, "The normalized pointer position is outside the SendInput range.");

        var input = new NativeInput
        {
            Type = 0,
            Mouse = new MouseInputData
            {
                Dx = (int)absoluteX,
                Dy = (int)absoluteY,
                MouseData = 0,
                Flags = 0xC001, // MOVE | ABSOLUTE | VIRTUALDESK; preserve the held button state.
                Time = 0,
                ExtraInfo = UIntPtr.Zero,
            },
        };
        var sent = SendInput(1, [input], Marshal.SizeOf<NativeInput>());
        var error = Marshal.GetLastWin32Error();
        Assert.AreEqual(1U, sent, $"Could not inject the pointer move to ({x}, {y}); Win32 error {error}.");

        var actual = default(PhysicalPoint);
        var arrived = WaitHelper.WaitForStable(
            observe: () =>
            {
                var read = GetPhysicalCursorPos(out actual);
                var readError = Marshal.GetLastWin32Error();
                Assert.IsTrue(read, $"Could not read the physical pointer position; Win32 error {readError}.");
                return actual.X == x && actual.Y == y;
            },
            isMatch: atTarget => atTarget,
            timeoutMS: 200,
            pollIntervalMS: 5);
        TestContext.WriteLine($"Physical pointer requested ({x}, {y}); actual ({actual.X}, {actual.Y})");
        Assert.IsTrue(arrived.Succeeded, "The physical pointer did not reach the requested position; the input scenario is invalid.");
    }

    private void AssertSettingsForeground(string stage)
    {
        var foreground = WindowControl.GetForegroundWindowInfo();
        TestContext.WriteLine($"Foreground {stage}: {foreground}");
        Assert.AreEqual(
            new IntPtr(Session.WindowHandle),
            foreground.Hwnd,
            $"Settings lost foreground {stage}; an external foreground change invalidated the input scenario.");
    }

    private void ScrollOuterPageDown(int x, int y)
    {
        AssertMovePhysicalPointer(x, y);
        MouseHelper.ScrollWheel(-30);
    }

    private static string ProfileName(int id) => $"{FixtureNamePrefix}{id:D2}";

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

    private void NavigateToProfiles(bool useVirtualizedList = false)
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

        Step("Scrolling the page until the profile list is visible");
        var viewport = WaitHelper.WaitForStable(
            observe: () => useVirtualizedList
                ? IsProfileButtonVisible(1, MoreButtonId)
                : AreAllProfilesVisible(Session.FindAll<Button>(By.AccessibilityId(ApplyButtonId), 1_000)),
            isMatch: visible => visible,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100,
            recover: _ => Find<Element>(By.AccessibilityId("PageScrollViewer"), 1_000).Scroll(ScrollDirection.Down));
        Assert.IsTrue(viewport.Succeeded, "The page did not scroll the profile list into view.");
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

    private void AssertStableOrder(string expectedOrder, int timeoutMS = 20_000)
    {
        Step($"Waiting for enabled profiles and stable UI/persisted order {expectedOrder}");
        var result = WaitHelper.WaitForStable(
            observe: ObserveOrder,
            isMatch: state => state is not null && state.Enabled && state.AllProfilesVisible && state.UiOrder == expectedOrder && state.PersistedOrder == expectedOrder,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is IOException || (ex is AssertFailedException && ex.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Succeeded, $"Expected order {expectedOrder} after save/reload. Last state: {result.LastObservation}; error: {result.LastException}");
    }

    private void AssertStablePersistedOrder(string expectedOrder)
    {
        Step($"Waiting for persisted order {expectedOrder}");
        var result = WaitHelper.WaitForStable(
            observe: ReadPersistedOrder,
            isMatch: order => order == expectedOrder,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is IOException);
        Assert.IsTrue(result.Succeeded, $"Expected persisted order {expectedOrder} after save. Last order: {result.LastObservation}; error: {result.LastException}");
    }

    private bool IsProfileButtonVisible(int profileId, string buttonId)
    {
        return FindVisibleProfileButton(profileId, buttonId) is not null;
    }

    private Button? FindVisibleProfileButton(int profileId, string buttonId)
    {
        var row = FindProfileRow(profileId);
        if (row is null || row.Width <= 0 || row.Height <= 0)
        {
            return null;
        }

        // Bounds only associate a candidate with this row; they do not prove full visibility.
        // Validate its HelpText and IsOffscreen instead of reading every virtualized button.
        var candidates = Session.FindAll<Button>(By.AccessibilityId(buttonId), 1_000)
            .Where(button => button.Width > 0 && button.Height > 0 &&
                button.X >= row.X && button.Y >= row.Y &&
                button.X + button.Width <= row.X + row.Width && button.Y + button.Height <= row.Y + row.Height)
            .ToArray();
        if (candidates.Length != 1)
        {
            return null;
        }

        var candidate = candidates[0];
        return candidate.HelpText.Contains(ProfileName(profileId), StringComparison.Ordinal) && IsButtonVisible(candidate)
            ? candidate
            : null;
    }

    private Element? FindProfileRow(int profileId)
    {
        return Session.FindAll<Element>(By.Name(ProfileName(profileId)), 1_000)
            .SingleOrDefault(element => string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase));
    }

    private OrderObservation ObserveOrder()
    {
        var buttons = Session.FindAll<Button>(By.AccessibilityId(ApplyButtonId), 1_000);
        var visibleOrder = buttons.OrderBy(button => button.Y).Select(button =>
        {
            var helpText = button.HelpText;
            return ProfileIds.SingleOrDefault(id => helpText.Contains(ProfileName(id), StringComparison.Ordinal));
        });
        var enabled = string.Equals(Find<Element>(By.AccessibilityId("ProfilesList"), 1_000).GetProperty("IsEnabled"), "True", StringComparison.OrdinalIgnoreCase);
        var allProfilesVisible = AreAllProfilesVisible(buttons);
        return new OrderObservation(string.Join(",", visibleOrder), ReadPersistedOrder(), enabled, allProfilesVisible);
    }

    private static string ReadPersistedOrder()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ProfilesPath));
        return string.Join(",", document.RootElement.GetProperty("profiles").EnumerateArray()
            .Select(profile => profile.GetProperty("id").GetInt32()));
    }

    private static bool AreAllProfilesVisible(IReadOnlyCollection<Button> buttons)
    {
        return buttons.Count == ProfileIds.Length && buttons.All(IsButtonVisible);
    }

    private static bool IsButtonVisible(Button button) =>
        button.Width > 0 && button.Height > 0 && string.Equals(button.GetProperty("IsOffscreen"), "False", StringComparison.OrdinalIgnoreCase);

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    private sealed record OrderObservation(string UiOrder, string PersistedOrder, bool Enabled, bool AllProfilesVisible);
}
