// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>
/// Coverage of what the File Locksmith window does once it is open: End task, Reload, automatic
/// delisting, recursive drive scans, and the elevation boundary.
/// </summary>
/// <remarks>
/// These tests start <c>PowerToys.FileLocksmithUI.exe</c> through the product's own paths-file IPC
/// instead of Explorer, so a failure points at the window rather than at the shell.
/// <see cref="FileLocksmithContextMenuTests"/> owns the context-menu surface.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class FileLocksmithProcessListTests : UITestBase
{
    private const int DriveScanTimeoutMS = 180_000;

    private static readonly string[] FileLocksmithModule = { FileLocksmithConstants.ModuleName };

    private readonly List<LockingProcessFixture> fixtures = new();

    public FileLocksmithProcessListTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: FileLocksmithModule)
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    protected override IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        FileLocksmithConstants.UiProcessName,
    };

    [TestInitialize]
    public void PrepareTest() => Assert.IsTrue(
        FileLocksmithUi.Close(),
        "A stale File Locksmith window could not be closed before the test.");

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        FileLocksmithUi.Close();

        foreach (var fixture in fixtures)
        {
            fixture.Dispose();
        }

        fixtures.Clear();
    }

    /// <summary>
    /// Checklist: End task on each listed process terminates it and removes its row, leaving the
    /// empty-list state behind.
    /// </summary>
    [TestMethod("FileLocksmith.List.EndTask")]
    [TestCategory("File Locksmith")]
    public void EndTaskTerminatesEachProcessAndRemovesItsRow()
    {
        var fixture = CreateFixture();
        fixture.Start(count: 2);

        var ui = FileLocksmithUi.Launch(fixture.TargetPath);
        AssertRowCount(ui, expected: 2, timeoutMS: 30_000, "File Locksmith did not list both locking processes.");

        for (var remaining = 2; remaining > 0; remaining--)
        {
            var endTaskLabels = FileLocksmithUi.EndTaskLabels(ui);
            Assert.AreEqual(remaining, endTaskLabels.Count, "Every listed process must offer its own End task button.");
            endTaskLabels[0].Click();

            Assert.IsTrue(
                fixture.WaitForRunningCount(remaining - 1, timeoutMS: 15_000),
                "End task did not terminate the process it was pressed for.");
            AssertRowCount(
                ui,
                expected: remaining - 1,
                timeoutMS: 15_000,
                "The row of the ended process was not removed from the list.");
        }

        Assert.IsTrue(
            ui.Has(By.Name(FileLocksmithConstants.EmptyListCaption), timeoutMS: 10_000),
            "File Locksmith did not fall back to its empty-list state after every process was ended.");
    }

    /// <summary>
    /// Checklist: a process started after the scan is only picked up once Reload is pressed.
    /// </summary>
    [TestMethod("FileLocksmith.List.Reload")]
    [TestCategory("File Locksmith")]
    public void ReloadRediscoversRestartedProcess()
    {
        var fixture = CreateFixture();
        fixture.Start();

        var ui = FileLocksmithUi.Launch(fixture.TargetPath);
        AssertRowCount(ui, expected: 1, timeoutMS: 30_000, "File Locksmith did not list the locking process.");

        fixture.KillOne();
        AssertRowCount(ui, expected: 0, timeoutMS: 15_000, "The exited process was not delisted.");

        fixture.Start();
        Assert.AreEqual(
            0,
            FileLocksmithUi.CountRows(ui, timeoutMS: 2_000),
            "File Locksmith listed a newly started process without being asked to rescan.");

        FileLocksmithUi.ClickReload(ui);
        AssertRowCount(ui, expected: 1, timeoutMS: 30_000, "Reload did not rediscover the restarted process.");
    }

    /// <summary>
    /// Checklist: closing a listed process delists it automatically, with no manual refresh.
    /// </summary>
    [TestMethod("FileLocksmith.List.AutoDelist")]
    [TestCategory("File Locksmith")]
    public void ExitedProcessIsDelistedWithoutReload()
    {
        var fixture = CreateFixture();
        fixture.Start(count: 2);

        var ui = FileLocksmithUi.Launch(fixture.TargetPath);
        AssertRowCount(ui, expected: 2, timeoutMS: 30_000, "File Locksmith did not list both locking processes.");

        fixture.KillOne();
        Assert.IsTrue(fixture.WaitForRunningCount(1, timeoutMS: 10_000), "The locking process did not exit.");
        AssertRowCount(
            ui,
            expected: 1,
            timeoutMS: 5_000,
            "File Locksmith did not delist the exited process on its own within 5s.");
    }

    /// <summary>
    /// Checklist: a drive-wide scan reports the processes locking files on that volume, and scrolling
    /// the (large) result list to the bottom and back does not crash File Locksmith.
    /// </summary>
    /// <remarks>
    /// The list virtualizes, so only the realized rows are in the UIA tree; the specific-process
    /// assertions live in the file and directory tests, and this one asserts the volume-wide scan
    /// produced rows at all and stayed alive while they were scrolled.
    /// </remarks>
    [TestMethod("FileLocksmith.List.DriveScan")]
    [TestCategory("File Locksmith")]
    public void DriveScanListsLockingProcessesAndSurvivesScrolling()
    {
        var fixture = CreateFixture();
        fixture.Start();

        var ui = FileLocksmithUi.Launch(DriveScanTimeoutMS, elevated: false, fixture.DriveRoot);
        AssertListedProcesses(ui, $"Scanning drive '{fixture.DriveRoot}' reported no locking processes at all.");

        ScrollListEndToEnd(ui);
        AssertUiAlive("File Locksmith did not survive scrolling the drive-wide process list.");

        if (!FileLocksmithUi.HostIsElevated)
        {
            TestContext.WriteLine(
                "Skipped the elevated repeat of the drive scan: elevating from a non-elevated test host " +
                "raises a UAC prompt that cannot be answered non-interactively.");
            return;
        }

        var elevatedUi = FileLocksmithUi.Launch(DriveScanTimeoutMS, elevated: true, fixture.DriveRoot);
        Assert.AreEqual(
            FileLocksmithConstants.ElevatedWindowTitle,
            FileLocksmithUi.CurrentWindowTitle(),
            "The relaunched File Locksmith did not report itself as elevated.");
        AssertListedProcesses(elevatedUi, "The elevated drive-wide scan reported no locking processes at all.");
        ScrollListEndToEnd(elevatedUi);
        AssertUiAlive("Elevated File Locksmith did not survive scrolling the drive-wide process list.");
    }

    /// <summary>
    /// Checklist: a non-elevated File Locksmith cannot see a higher-integrity process and offers
    /// "Restart as administrator"; the elevated window drops that button and does see the process.
    /// </summary>
    [TestMethod("FileLocksmith.List.Elevation")]
    [TestCategory("File Locksmith")]
    public void NonElevatedInstanceCannotSeeElevatedProcess()
    {
        var fixture = CreateFixture();
        fixture.Start();

        var ui = FileLocksmithUi.Launch(fixture.TargetPath);
        Assert.AreEqual(
            FileLocksmithConstants.WindowTitle,
            FileLocksmithUi.CurrentWindowTitle(),
            "File Locksmith did not start as the non-elevated window.");
        Assert.IsTrue(
            FileLocksmithUi.HasRestartAsAdminButton(ui),
            "A non-elevated File Locksmith must offer 'Restart as administrator'.");
        AssertRowCount(ui, expected: 1, timeoutMS: 30_000, "File Locksmith did not list the locking process.");

        if (!FileLocksmithUi.HostIsElevated)
        {
            TestContext.WriteLine(
                "Skipped the elevated half: an elevated locking process and the 'Restart as administrator' " +
                "relaunch both need an elevated test host (otherwise UAC prompts block the run).");
            return;
        }

        fixture.StartElevated();
        FileLocksmithUi.ClickReload(ui);
        AssertRowCount(
            ui,
            expected: 1,
            timeoutMS: 30_000,
            "A non-elevated File Locksmith listed a higher-integrity process it cannot inspect.");

        var elevatedUi = FileLocksmithUi.LaunchElevated(fixture.TargetPath);
        Assert.AreEqual(
            FileLocksmithConstants.ElevatedWindowTitle,
            FileLocksmithUi.CurrentWindowTitle(),
            "The elevated File Locksmith did not use its administrator window title.");
        Assert.IsFalse(
            FileLocksmithUi.HasRestartAsAdminButton(elevatedUi),
            "An elevated File Locksmith must hide 'Restart as administrator'.");
        AssertRowCount(
            elevatedUi,
            expected: 2,
            timeoutMS: 30_000,
            "An elevated File Locksmith did not see the higher-integrity locking process.");
    }

    private static void ScrollListEndToEnd(Session ui)
    {
        var list = ui.Find<Element>(By.AccessibilityId(FileLocksmithConstants.ProcessListAutomationId), timeoutMS: 15_000);
        for (var pass = 0; pass < 2; pass++)
        {
            list.ScrollToEdge(toBottom: true);
            Thread.Sleep(500);
            list.ScrollToEdge(toBottom: false);
            Thread.Sleep(500);
        }
    }

    private static void AssertRowCount(Session ui, int expected, int timeoutMS, string message)
    {
        var settled = expected > 0
            ? FileLocksmithUi.WaitForRowCountWithReload(ui, expected, timeoutMS)
            : FileLocksmithUi.WaitForRowCount(ui, expected, timeoutMS);
        Assert.IsTrue(
            settled,
            $"{message} Expected {expected} row(s), found {FileLocksmithUi.CountRows(ui, timeoutMS: 2_000)}.");
        if (expected > 0)
        {
            Assert.IsTrue(
                FileLocksmithUi.HasProcessRow(ui, LockingProcessFixture.LockerFileName),
                $"The listed rows were not headed by '{LockingProcessFixture.LockerFileName}'.");
        }
    }

    private static void AssertUiAlive(string message) => Assert.IsTrue(
        FileLocksmithUi.WaitForProcess(FileLocksmithConstants.UiProcessName, expected: true, timeoutMS: 2_000),
        message);

    private static void AssertListedProcesses(Session ui, string message) => Assert.IsTrue(
        FileLocksmithUi.CountRows(ui, timeoutMS: 30_000) > 0,
        message);

    private LockingProcessFixture CreateFixture()
    {
        var fixture = new LockingProcessFixture();
        fixtures.Add(fixture);
        return fixture;
    }
}
