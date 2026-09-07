// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>
/// Explorer-driven coverage of the File Locksmith release checklist: the
/// "Unlock with File Locksmith" command on a file, a folder and a drive, and the fact that the
/// Settings toggle gates it out of both context-menu tiers without breaking the menu itself.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class FileLocksmithContextMenuTests : UITestBase
{
    private const string ClassicHandlerKeyPath =
        @"Software\Classes\AllFileSystemObjects\ShellEx\ContextMenuHandlers\FileLocksmithExt";

    private const string ModernPackageName = "FileLocksmithContextMenu";

    private static readonly string[] ShellIntegrationModules =
    {
        FileLocksmithConstants.ModuleName,
        FileLocksmithConstants.PowerRenameModuleName,
    };

    private readonly List<LockingProcessFixture> fixtures = new();

    public FileLocksmithContextMenuTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ShellIntegrationModules)
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
    public void PrepareTest()
    {
        Assert.IsTrue(FileLocksmithUi.Close(), "A stale File Locksmith window could not be closed before the test.");
        Assert.IsTrue(ExplorerHelper.CloseFileWindows(), "Stale Explorer file windows could not be closed before the test.");

        // Both handlers register when the runner enables the module; give that a moment to land.
        WaitUntil(() => DefaultTierRegistered() || ClassicHandlerRegistered(), timeoutMS: 30_000);
    }

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        FileLocksmithUi.Close();
        ExplorerHelper.CloseFileWindows();

        foreach (var fixture in fixtures)
        {
            fixture.Dispose();
        }

        fixtures.Clear();
    }

    /// <summary>
    /// Checklist: right-click an executable that is currently running twice, confirm
    /// "Unlock with File Locksmith" is present, and that invoking it opens the File Locksmith window
    /// listing one row (with an "End task" button) per process holding the file.
    /// </summary>
    [TestMethod("FileLocksmith.ContextMenu.LaunchOnLockedFile")]
    [TestCategory("File Locksmith")]
    public void ContextMenuLaunchesFileLocksmithForLockedFile()
    {
        RequireDefaultTier();
        var fixture = CreateFixture();
        fixture.Start(count: 2);

        var explorer = ExplorerHelper.OpenFolder(fixture.TargetFolder);
        ExplorerHelper.InvokeCommand(
            explorer,
            () => ExplorerHelper.OpenFolder(fixture.TargetFolder),
            new[] { fixture.TargetPath },
            ContextMenuTier.Default,
            FileLocksmithConstants.ContextMenuCaption);

        var ui = FileLocksmithUi.WaitForWindow(loadTimeoutMS: 60_000);
        Assert.AreEqual(
            FileLocksmithConstants.WindowTitle,
            FileLocksmithUi.CurrentWindowTitle(),
            "File Locksmith launched from the context menu was not the expected non-elevated window.");
        Assert.IsTrue(
            FileLocksmithUi.WaitForRowCount(ui, expected: 2, timeoutMS: 30_000),
            $"File Locksmith listed {FileLocksmithUi.CountRows(ui)} row(s) for a file locked by 2 processes.");
        Assert.IsTrue(
            FileLocksmithUi.HasProcessRow(ui, LockingProcessFixture.LockerFileName),
            $"The listed rows were not headed by '{LockingProcessFixture.LockerFileName}'.");
        Assert.AreEqual(
            2,
            FileLocksmithUi.CountRows(ui),
            "Each listed process must offer its own End task button.");
    }

    /// <summary>
    /// Checklist: right-click the directory containing the executable and confirm the command is
    /// present and lists the process(es) found recursively inside that directory tree.
    /// </summary>
    [TestMethod("FileLocksmith.ContextMenu.Directory")]
    [TestCategory("File Locksmith")]
    public void ContextMenuScansDirectoryRecursively()
    {
        RequireDefaultTier();

        // The locked file lives one level below the folder under test, so only a recursive scan finds it.
        var fixture = CreateFixture(targetSubFolder: "nested");
        fixture.Start();

        var parentFolder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(fixture.RootFolder))!;
        var explorer = ExplorerHelper.OpenFolder(parentFolder);
        ExplorerHelper.InvokeCommand(
            explorer,
            () => ExplorerHelper.OpenFolder(parentFolder),
            new[] { fixture.RootFolder },
            ContextMenuTier.Default,
            FileLocksmithConstants.ContextMenuCaption);

        var ui = FileLocksmithUi.WaitForWindow(loadTimeoutMS: 60_000);
        Assert.IsTrue(
            FileLocksmithUi.WaitForRowCount(ui, expected: 1, timeoutMS: 30_000),
            "File Locksmith did not report the process locking a file nested inside the selected directory.");
        Assert.IsTrue(
            FileLocksmithUi.HasProcessRow(ui, LockingProcessFixture.LockerFileName),
            $"The listed row was not headed by '{LockingProcessFixture.LockerFileName}'.");
    }

    /// <summary>
    /// Checklist: right-click the drive holding the executable and confirm the command is present.
    /// The sparse package only registers <c>Directory</c> and <c>*</c> item types, so on Windows 11
    /// the drive command lives in the classic ("Show more options") menu.
    /// </summary>
    [TestMethod("FileLocksmith.ContextMenu.DriveRoot")]
    [TestCategory("File Locksmith")]
    public void ContextMenuIsAvailableOnDriveRoot()
    {
        RequireClassicTier();
        var fixture = CreateFixture();
        fixture.Start();

        var explorer = ExplorerHelper.OpenThisPc();
        var probe = ExplorerHelper.ProbeCommand(
            explorer,
            ExplorerHelper.OpenThisPc,
            new[] { fixture.DriveRoot },
            ContextMenuTier.Classic,
            FileLocksmithConstants.ContextMenuCaption,
            expectedCommand: true,
            siblingCaption: null,
            TestContext);

        Assert.IsTrue(probe.Last.IsOpen, "The classic context menu for the drive did not become ready.");
        Assert.IsTrue(
            probe.Succeeded,
            $"The classic context menu for drive '{fixture.DriveRoot}' did not show " +
            $"'{FileLocksmithConstants.ContextMenuCaption}'.");
    }

    /// <summary>
    /// Checklist: disabling File Locksmith removes its command from the tier-1 menu and from
    /// "Show more options", while a sibling PowerToys command stays put — proving the menu still
    /// renders and only File Locksmith was gated out. Re-enabling brings it back in both menus.
    /// </summary>
    [TestMethod("FileLocksmith.ContextMenu.EnabledState")]
    [TestCategory("File Locksmith")]
    public void ContextMenuTracksModuleEnabledState()
    {
        RequireDefaultTier();
        var settings = NavigateToFileLocksmithSettings();
        var toggle = settings.Find<ToggleSwitch>(By.Name(FileLocksmithConstants.ModuleName));
        Assert.IsTrue(toggle.IsOn, "File Locksmith did not start from the deterministic enabled baseline.");

        var fixture = CreateFixture();
        var folder = fixture.TargetFolder;
        var selection = new[] { fixture.TargetPath };
        var tiers = ClassicHandlerRegistered() && ExplorerHelper.IsWindows11OrNewer
            ? new[] { ContextMenuTier.Default, ContextMenuTier.Classic }
            : new[] { ContextMenuTier.Default };

        try
        {
            foreach (var tier in tiers)
            {
                AssertCommandPresence(folder, selection, tier, expected: true);
            }

            toggle = SetModuleEnabled(toggle, false);
            foreach (var tier in tiers)
            {
                AssertCommandPresence(folder, selection, tier, expected: false);
            }

            toggle = SetModuleEnabled(toggle, true);
            foreach (var tier in tiers)
            {
                AssertCommandPresence(folder, selection, tier, expected: true);
            }
        }
        finally
        {
            try
            {
                SetModuleEnabled(toggle, true);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Restoring the File Locksmith toggle failed; restarting the scope. {ex.Message}");
                RestartScope(ShellIntegrationModules);
            }
        }
    }

    private static bool ClassicHandlerRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ClassicHandlerKeyPath);
        return key is not null;
    }

    /// <summary>The surface a plain right-click shows: tier-1 on Windows 11, classic on Windows 10.</summary>
    private static bool DefaultTierRegistered() =>
        ExplorerHelper.IsWindows11OrNewer ? ModernPackageRegistered() : ClassicHandlerRegistered();

    private static void RequireDefaultTier()
    {
        if (DefaultTierRegistered())
        {
            return;
        }

        Assert.Inconclusive(
            ExplorerHelper.IsWindows11OrNewer
                ? "The FileLocksmithContextMenu sparse package is not registered, so no Windows 11 tier-1 " +
                  "command can appear. Unsigned builds fail to register it (0x800B0100) — sign the .msix and " +
                  "trust the signer (see .pipelines/signSparsePackages.ps1)."
                : "The classic File Locksmith context-menu handler is not registered. It is compiled out of " +
                  "Debug builds — build Release, or define ENABLE_REGISTRATION for FileLocksmithExt.");
    }

    private static void RequireClassicTier()
    {
        if (ClassicHandlerRegistered())
        {
            return;
        }

        Assert.Inconclusive(
            "The classic (registry-COM) File Locksmith handler is not registered, so no drive command can " +
            "exist. It is compiled out of Debug builds — build Release, or define ENABLE_REGISTRATION.");
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMS, int pollIntervalMS = 1_000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(pollIntervalMS);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static bool ModernPackageRegistered()
    {
        try
        {
            return new Windows.Management.Deployment.PackageManager()
                .FindPackagesForUser(string.Empty)
                .Any(package => package.Id.Name.Contains(ModernPackageName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool WaitForElementSearch(Session session, By by, int timeoutMS) =>
        session.WaitFor(
            () => session.Has(by, timeoutMS: 500),
            timeoutMS: timeoutMS,
            pollIntervalMS: 200);

    private static Session NavigateToFileLocksmithSettings()
    {
        var settings = Session.FromProcess(
            "PowerToys.Settings",
            PowerToysModule.PowerToysSettings,
            timeoutMS: 15_000);
        if (WaitForElementSearch(settings, By.AccessibilityId("FileLocksmithEnableFileLocksmith"), timeoutMS: 5_000))
        {
            return settings;
        }

        if (!WaitForElementSearch(settings, By.AccessibilityId("FileLocksmithNavItem"), timeoutMS: 5_000))
        {
            settings.Find<NavigationViewItem>(By.AccessibilityId("FileManagementNavItem")).Click(msPostAction: 500);
            Assert.IsTrue(
                WaitForElementSearch(settings, By.AccessibilityId("FileLocksmithNavItem"), timeoutMS: 5_000),
                "The File Management navigation group did not expose File Locksmith.");
        }

        settings.Find<NavigationViewItem>(By.AccessibilityId("FileLocksmithNavItem")).Click(msPostAction: 500);
        Assert.IsTrue(
            WaitForElementSearch(settings, By.AccessibilityId("FileLocksmithEnableFileLocksmith"), timeoutMS: 60_000),
            "The File Locksmith settings page did not become ready.");
        return settings;
    }

    private static ToggleSwitch SetModuleEnabled(ToggleSwitch toggle, bool enabled)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                toggle.Toggle(enabled);
                Assert.IsTrue(
                    toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", timeoutMS: 5_000),
                    $"The File Locksmith enable switch did not settle to {(enabled ? "On" : "Off")}.");
                return toggle;
            }
            catch (TimeoutException) when (attempt < 2)
            {
                var settings = Session.FromProcess(
                    "PowerToys.Settings",
                    PowerToysModule.PowerToysSettings,
                    timeoutMS: 15_000);
                toggle = settings.Find<ToggleSwitch>(By.Name(FileLocksmithConstants.ModuleName), timeoutMS: 15_000);
            }
        }

        return toggle;
    }

    private void AssertCommandPresence(string folder, string[] selection, ContextMenuTier tier, bool expected)
    {
        var probe = ExplorerHelper.ProbeCommand(
            ExplorerHelper.OpenFolder(folder),
            () => ExplorerHelper.OpenFolder(folder),
            selection,
            tier,
            FileLocksmithConstants.ContextMenuCaption,
            expected,
            siblingCaption: FileLocksmithConstants.PowerRenameContextMenuCaption,
            TestContext);

        var surface = tier == ContextMenuTier.Classic ? "classic" : "default";
        Assert.IsTrue(probe.Last.IsOpen, $"The {surface} Explorer context menu did not become ready.");
        Assert.IsTrue(
            probe.Last.HasSibling,
            $"The {surface} Explorer context menu did not render the sibling PowerRename command, so its " +
            $"'{FileLocksmithConstants.ContextMenuCaption}' state cannot be trusted.");
        Assert.AreEqual(
            expected,
            probe.Last.HasCommand,
            $"The {surface} Explorer context menu did {(expected ? "not show" : "show")} " +
            $"'{FileLocksmithConstants.ContextMenuCaption}'.");
    }

    private LockingProcessFixture CreateFixture(string? targetSubFolder = null)
    {
        var fixture = new LockingProcessFixture(targetSubFolder);
        fixtures.Add(fixture);
        return fixture;
    }
}
