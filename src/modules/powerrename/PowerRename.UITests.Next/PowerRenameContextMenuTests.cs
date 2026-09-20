// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// PowerRename's Explorer integration: the entry's presence on both context-menu surfaces, its icon,
/// the extended-menu-only setting, and the real invoke path into the PowerRename window.
/// </summary>
/// <remarks>Covers checklist items 1-3 of microsoft/PowerToys#40663.</remarks>
[TestClass]
[DoNotParallelize]
public sealed partial class PowerRenameTests : PowerRenameTestBase
{
    private const string ModernPackageName = "PowerRenameContextMenu";
    private const string ModuleToggleName = "PowerRename";
    private const int ExplorerTimeoutMS = 30_000;
    private const int MenuSurfaceTimeoutMS = 25_000;
    private const int MenuAttemptTimeoutMS = 90_000;

    private static bool explorerRefreshedForRegistration;
    private bool contextMenuTest;

    [TestCleanup]
    public async Task CleanupContextMenuTest()
    {
        if (!contextMenuTest)
        {
            return;
        }

        // Capture first: the base cleanup runs last, and by then Explorer is already gone.
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        KeyboardHelper.SendKeys(Key.Esc);
        ExplorerControl.CloseFileWindows();
    }

    [TestMethod("PowerRename.ContextMenu.EnabledState")]
    [TestCategory("PowerRename")]
    public void ContextMenuTracksModuleEnabledState()
    {
        PrepareContextMenuTest();

        // Checklist item 1 — and on Windows 11 both the tier-1 and the classic surface must carry it.
        var settings = NavigateToPowerRenameSettings();
        var toggle = FindExact<ToggleSwitch>(settings, ModuleToggleName, timeoutMS: 15_000);
        Assert.IsNotNull(toggle, "The PowerRename settings page did not expose its enable switch.");
        Assert.IsTrue(toggle!.IsOn, "PowerRename did not start from the deterministic enabled baseline.");

        var folder = CreateTestFolder();
        var fixture = CreateFile(folder, "context-menu.txt");

        try
        {
            toggle = SetModuleEnabled(toggle, false);
            var explorer = OpenExplorer(folder);
            AssertClassicMenuContainsEntry(explorer, new[] { fixture }, expected: false, extendedVerbs: false);

            toggle = SetModuleEnabled(toggle, true);
            Assert.IsTrue(
                WaitForModernPackageRegistration(timeoutMS: 30_000),
                "The PowerRename sparse context-menu package did not register after the module was re-enabled.");
            explorer = OpenExplorer(folder, forceHandlerRefresh: true);
            AssertClassicMenuContainsEntry(explorer, new[] { fixture }, expected: true, extendedVerbs: false);
            AssertModernMenuContainsEntry(explorer, new[] { fixture }, expected: true);
        }
        finally
        {
            try
            {
                SetModuleEnabled(toggle, true);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Restoring the PowerRename toggle failed; restarting the scope. {ex.Message}");
                RestartScope();
            }
        }
    }

    [TestMethod("PowerRename.ContextMenu.ShowIcon")]
    [TestCategory("PowerRename")]
    public void ContextMenuIconFollowsShowIconSetting()
    {
        PrepareContextMenuTest();

        // Checklist item 2. The icon lives in MENUITEMINFO.hbmpItem, which GetMenuItemInfo will not
        // hand across a process boundary (every item of Explorer's menu reads back as 0), so the
        // assertion is on the pixels of the entry's icon gutter instead.
        var folder = CreateTestFolder();
        var fixture = CreateFile(folder, "icon.txt");
        var explorer = OpenExplorer(folder);

        AssertMenuIconSetting(explorer, fixture, ContextMenuSurface.Classic);
        if (ModernSurfaceAvailable())
        {
            AssertMenuIconSetting(explorer, fixture, ContextMenuSurface.Modern);
        }
    }

    [TestMethod("PowerRename.ContextMenu.ExtendedOnly")]
    [TestCategory("PowerRename")]
    public void ContextMenuHonorsExtendedContextMenuOnlySetting()
    {
        PrepareContextMenuTest();

        // Checklist item 3 — the entry moves out of the plain classic menu into the extended one.
        var folder = CreateTestFolder();
        var fixture = CreateFile(folder, "extended.txt");
        var explorer = OpenExplorer(folder);

        ConfigureModuleSettings(extendedContextMenuOnly: false);
        AssertClassicMenuContainsEntry(explorer, new[] { fixture }, expected: true, extendedVerbs: false);
        AssertModernMenuContainsEntry(explorer, new[] { fixture }, expected: true);

        ConfigureModuleSettings(extendedContextMenuOnly: true);
        AssertClassicMenuContainsEntry(explorer, new[] { fixture }, expected: false, extendedVerbs: false);
        AssertClassicMenuContainsEntry(explorer, new[] { fixture }, expected: true, extendedVerbs: true);
    }

    [TestMethod("PowerRename.ContextMenu.OpensWindowWithSelection")]
    [TestCategory("PowerRename")]
    public void InvokingTheContextMenuOpensPowerRenameWithTheSelection()
    {
        PrepareContextMenuTest();

        // The real user path: Explorer streams the selection to the UI over a pipe, not on argv.
        var folder = CreateTestFolder();
        var first = CreateFile(folder, "alpha.txt");
        var second = CreateFile(folder, "beta.txt");
        var explorer = OpenExplorer(folder);

        var menu = OpenMenuWithRetry(
            explorer,
            new[] { first, second },
            ModernSurfaceAvailable() ? ContextMenuSurface.Modern : ContextMenuSurface.Classic,
            extendedVerbs: false,
            requireEntry: true);
        var entry = ShellMenu.FindVisibleMenuItem(menu, ContextMenuCaption, timeoutMS: MenuSurfaceTimeoutMS);
        Assert.IsNotNull(entry, $"Explorer did not offer '{ContextMenuCaption}' for the selected files.");
        Step($"Invoking '{ContextMenuCaption}'");
        entry!.Invoke(msPostAction: 500);

        var window = WindowsFinder.WaitForWindowByApp(
            PowerRenameProcessName,
            info => info.Width > 0 && info.Height > 0,
            timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(window, "The PowerRename window did not open from the context menu.");

        var session = Session.FromProcess(PowerRenameProcessName, PowerToysModule.PowerRename, timeoutMS: WindowTimeoutMS);
        Assert.IsTrue(
            session.WaitFor(
                () => session.Has(By.AccessibilityId(SearchBoxAutomationId), timeoutMS: 1_000),
                WindowTimeoutMS,
                pollIntervalMS: 500),
            "The PowerRename window opened from the context menu but never became ready.");

        Assert.IsTrue(
            session.WaitFor(
                () => FindRowCheckBox(session, "alpha.txt", 500) is not null && FindRowCheckBox(session, "beta.txt", 500) is not null,
                timeoutMS: PreviewTimeoutMS,
                pollIntervalMS: 250),
            "The PowerRename window did not list both selected files.");
    }

    // ---- settings navigation --------------------------------------------------------------------

    private static Session NavigateToPowerRenameSettings()
    {
        var settings = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings, timeoutMS: 15_000);
        if (WaitForElement(settings, By.AccessibilityId("PowerRenameNavItem"), timeoutMS: 5_000) == false)
        {
            settings.Find<NavigationViewItem>(By.AccessibilityId("FileManagementNavItem")).Click(msPostAction: 500);
            Assert.IsTrue(
                WaitForElement(settings, By.AccessibilityId("PowerRenameNavItem"), timeoutMS: 10_000),
                "The File Management navigation group did not expose PowerRename.");
        }

        settings.Find<NavigationViewItem>(By.AccessibilityId("PowerRenameNavItem")).Click(msPostAction: 500);
        Assert.IsTrue(
            WaitForElement(settings, By.AccessibilityId("PowerRenameToggleAutoComplete"), timeoutMS: 60_000) ||
            WaitForElement(settings, By.Name(ModuleToggleName), timeoutMS: 10_000),
            "The PowerRename settings page did not become ready.");
        return settings;
    }

    private static bool WaitForElement(Session session, By by, int timeoutMS) =>
        session.WaitFor(() => session.Has(by, timeoutMS: 500), timeoutMS: timeoutMS, pollIntervalMS: 200);

    private static ToggleSwitch SetModuleEnabled(ToggleSwitch toggle, bool enabled)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                toggle.Toggle(enabled);
                if (!toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", timeoutMS: 5_000))
                {
                    throw new TimeoutException($"The PowerRename enable switch did not settle to {(enabled ? "On" : "Off")}.");
                }

                if (!WaitForModuleEnabledSetting(enabled, timeoutMS: 15_000))
                {
                    throw new TimeoutException($"settings.json did not persist enabled.PowerRename={enabled}.");
                }

                return toggle;
            }
            catch (TimeoutException) when (attempt < 2)
            {
                var settings = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings, timeoutMS: 15_000);
                toggle = settings.Find<ToggleSwitch>(By.Name(ModuleToggleName), timeoutMS: 15_000);
            }
        }

        return toggle;
    }

    private static bool WaitForModuleEnabledSetting(bool expected, int timeoutMS) =>
        WaitHelper.WaitForStable(
            observe: ReadModuleEnabledSetting,
            isMatch: enabled => enabled == expected,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250).Succeeded;

    private static bool? ReadModuleEnabledSetting()
    {
        try
        {
            var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root?["enabled"]?["PowerRename"]?.GetValue<bool>();
        }
        catch
        {
            return null;
        }
    }

    private static bool WaitForModernPackageRegistration(int timeoutMS)
    {
        if (!IsWindows11OrNewer)
        {
            return true;
        }

        return WaitHelper.WaitForStable(
            observe: ModernPackageRegistered,
            isMatch: registered => registered,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250).Succeeded;
    }

    private static bool ModernPackageRegistered()
    {
        try
        {
            return new Windows.Management.Deployment.PackageManager()
                .FindPackagesForUser(string.Empty)
                .Any(package => package.Id.Name.Contains(ModernPackageName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    // ---- context-menu assertions -----------------------------------------------------------------

    private enum ContextMenuSurface
    {
        /// <summary>The Windows 11 tier-1 (sparse-MSIX, <c>IExplorerCommand</c>) menu.</summary>
        Modern,

        /// <summary>The classic <c>#32768</c> menu, reached through "Show more options" on Windows 11.</summary>
        Classic,
    }

    /// <summary>Whether this OS/build actually shows the tier-1 surface.</summary>
    private static bool ModernSurfaceAvailable() => IsWindows11OrNewer && ModernPackageRegistered();

    private void AssertModernMenuContainsEntry(Session explorer, string[] paths, bool expected)
    {
        // The tier-1 surface only exists on Windows 11, and only once its sparse package registered —
        // which needs a signed build. Skipping it on an unsigned build keeps the classic assertions
        // meaningful instead of failing every Windows 11 run.
        if (!ModernSurfaceAvailable())
        {
            Step("Skipping the tier-1 context menu: its sparse package is not registered on this build.");
            return;
        }

        var menu = OpenMenuWithRetry(explorer, paths, ContextMenuSurface.Modern, extendedVerbs: false, requireEntry: expected);
        try
        {
            var observation = WaitHelper.WaitForStable(
                observe: () => ShellMenu.FindVisibleMenuItem(menu, ContextMenuCaption, timeoutMS: 250) is not null,
                isMatch: present => present == expected,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: expected ? 2 : 8,
                pollIntervalMS: 250);
            Assert.IsTrue(
                observation.Succeeded,
                $"The tier-1 Explorer context menu did {(expected ? "not show" : "show")} '{ContextMenuCaption}'.");
        }
        finally
        {
            KeyboardHelper.SendKeys(Key.Esc);
        }
    }

    private void AssertClassicMenuContainsEntry(Session explorer, string[] paths, bool expected, bool extendedVerbs)
    {
        var menu = OpenMenuWithRetry(explorer, paths, ContextMenuSurface.Classic, extendedVerbs, requireEntry: expected);
        try
        {
            var observation = WaitHelper.WaitForStable(
                observe: () => ShellMenu.TryReadClassicItemCaptions(new IntPtr(menu.WindowHandle)),
                isMatch: captions => captions is not null && HasEntry(captions) == expected,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: expected ? 2 : 8,
                pollIntervalMS: 250);
            Assert.IsTrue(
                observation.Succeeded,
                $"The classic Explorer context menu ({(extendedVerbs ? "extended" : "plain")}) did " +
                $"{(expected ? "not show" : "show")} '{ContextMenuCaption}'. {Describe(observation.LastObservation)}");
        }
        finally
        {
            KeyboardHelper.SendKeys(Key.Esc);
        }
    }

    private static bool HasEntry(IReadOnlyList<string>? captions) =>
        captions?.Any(caption => caption.Equals(ContextMenuCaption, StringComparison.OrdinalIgnoreCase)) == true;

    private static string Describe(IReadOnlyList<string>? captions) =>
        captions is null
            ? "Menu items: <not read>."
            : $"Menu items: [{string.Join(", ", captions.Select(caption => $"'{caption}'"))}].";

    /// <summary>
    /// Crop of the PowerRename entry's row, taken from the live desktop so the popup menu (which no
    /// window-scoped capture reaches) is included.
    /// </summary>
    private void AssertMenuIconSetting(Session explorer, string fixture, ContextMenuSurface surface)
    {
        var surfaceName = surface.ToString().ToLowerInvariant();
        var withIcon = CaptureMenuEntry(explorer, fixture, showIcon: true, $"{surfaceName}-icon-on", surface);
        var withoutIcon = CaptureMenuEntry(explorer, fixture, showIcon: false, $"{surfaceName}-icon-off", surface);

        var iconPixels = CountGutterDetailPixels(withIcon);
        var plainPixels = CountGutterDetailPixels(withoutIcon);
        Step($"{surface} icon gutter detail pixels: on={iconPixels}, off={plainPixels}");

        Assert.IsTrue(
            plainPixels < 8,
            $"The {surface} entry's icon gutter had {plainPixels} non-background pixels while the icon setting was off.");
        Assert.IsTrue(
            iconPixels > plainPixels + 20,
            $"The {surface} entry showed no icon difference (on={iconPixels}, off={plainPixels} non-background pixels).");
    }

    private string CaptureMenuEntry(Session explorer, string fixture, bool showIcon, string name, ContextMenuSurface surface)
    {
        ConfigureModuleSettings(showIcon: showIcon);
        var menu = OpenMenuWithRetry(explorer, new[] { fixture }, surface, extendedVerbs: false, requireEntry: true);
        var results = TestContext.TestResultsDirectory ?? Path.GetTempPath();
        var desktopPath = Path.Combine(results, $"context-menu-{name}-desktop.png");
        var entryPath = Path.Combine(results, $"context-menu-{name}.png");

        try
        {
            var entry = ShellMenu.FindVisibleMenuItem(menu, ContextMenuCaption, timeoutMS: MenuSurfaceTimeoutMS);
            Assert.IsNotNull(
                entry,
                $"The {surface} Explorer context menu did not show '{ContextMenuCaption}'. " +
                (surface == ContextMenuSurface.Classic
                    ? Describe(ShellMenu.TryReadClassicItemCaptions(new IntPtr(menu.WindowHandle)))
                    : string.Empty));
            Assert.IsTrue(ScreenCapture.TryCaptureDesktop(desktopPath), "The desktop could not be captured while the menu was open.");

            using (var desktop = new Bitmap(desktopPath))
            {
                var bounds = Rectangle.Intersect(
                    new Rectangle(entry!.X, entry.Y, entry.Width, entry.Height),
                    new Rectangle(0, 0, desktop.Width, desktop.Height));
                Assert.IsTrue(
                    bounds.Width > 0 && bounds.Height > 0,
                    $"The '{ContextMenuCaption}' entry reported an off-screen rectangle " +
                    $"({entry.X},{entry.Y},{entry.Width},{entry.Height}) on a {desktop.Width}x{desktop.Height} desktop.");

                using var crop = desktop.Clone(bounds, desktop.PixelFormat);
                crop.Save(entryPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            TestContext.AddResultFile(entryPath);
            return entryPath;
        }
        finally
        {
            KeyboardHelper.SendKeys(Key.Esc);
            if (File.Exists(desktopPath))
            {
                File.Delete(desktopPath);
            }
        }
    }

    /// <summary>
    /// Pixels in the entry's icon gutter that differ from the gutter's dominant (background) colour.
    /// An entry with an icon paints tens of them; an entry without paints none.
    /// </summary>
    private static int CountGutterDetailPixels(string imagePath)
    {
        using var image = new Bitmap(imagePath);
        var gutterWidth = Math.Max(1, Math.Min(24, image.Width / 3));
        var counts = new Dictionary<int, int>();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < gutterWidth; x++)
            {
                var key = image.GetPixel(x, y).ToArgb();
                counts[key] = counts.TryGetValue(key, out var seen) ? seen + 1 : 1;
            }
        }

        var background = Color.FromArgb(counts.OrderByDescending(pair => pair.Value).First().Key);
        var detail = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < gutterWidth; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (Math.Abs(pixel.R - background.R) > 30 ||
                    Math.Abs(pixel.G - background.G) > 30 ||
                    Math.Abs(pixel.B - background.B) > 30)
                {
                    detail++;
                }
            }
        }

        return detail;
    }

    /// <summary>
    /// Open the requested context-menu surface, re-establishing the Explorer selection before every
    /// attempt and reopening a stale window: a slow agent re-renders the file view asynchronously and
    /// silently drops the selection the gesture needs.
    /// </summary>
    /// <param name="requireEntry">
    /// When the entry is expected, treat a menu that opened without it as a failed attempt and reopen.
    /// A shell extension's item can be enumerated after the popup is already on screen, so one open is
    /// not enough to conclude the entry is missing.
    /// </param>
    private Session OpenMenuWithRetry(Session explorer, string[] paths, ContextMenuSurface surface, bool extendedVerbs, bool requireEntry = false)
    {
        var folder = Path.GetDirectoryName(paths[0])!;
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(MenuAttemptTimeoutMS);
        var selectionFailures = 0;

        do
        {
            KeyboardHelper.SendKeys(Key.Esc);
            var selected = TrySelectStable(explorer, paths);
            if (selected is null)
            {
                if (++selectionFailures >= 2)
                {
                    selectionFailures = 0;
                    explorer = OpenExplorer(folder);
                }

                Thread.Sleep(300);
                continue;
            }

            selectionFailures = 0;
            explorer = selected;

            var menu = surface == ContextMenuSurface.Classic
                ? OpenClassicMenu(explorer, extendedVerbs)
                : OpenModernMenu(explorer);
            if (menu is not null && (!requireEntry || MenuHasEntry(menu, surface)))
            {
                return menu;
            }

            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail(
            $"Explorer never opened the {surface} context menu" +
            $"{(requireEntry ? $" carrying '{ContextMenuCaption}'" : string.Empty)} " +
            $"for [{string.Join(", ", paths.Select(Path.GetFileName))}]. " +
            $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return null!;
    }

    private static bool MenuHasEntry(Session menu, ContextMenuSurface surface) =>
        surface == ContextMenuSurface.Classic
            ? HasEntry(ShellMenu.TryReadClassicItemCaptions(new IntPtr(menu.WindowHandle)))
            : ShellMenu.FindVisibleMenuItem(menu, ContextMenuCaption, timeoutMS: 5_000) is not null;

    private Session? OpenModernMenu(Session explorer)
    {
        TryEnsureExplorerForeground(explorer);
        Step("Opening the tier-1 Explorer context menu");
        var menu = ShellMenu.OpenForFocusedControl(explorer, timeoutMS: MenuSurfaceTimeoutMS);
        return menu is not null && WindowsFinder.ListByApp(ExplorerControl.ProcessName).Any(window =>
            window.Hwnd == menu.WindowHandle && ShellMenu.IsModernWindow(window))
            ? menu
            : null;
    }

    /// <summary>
    /// Reach the classic menu on either OS. Windows 11 shows the tier-1 menu first, so the classic one
    /// is one "Show more options" away — unless Shift is held, which takes the shell straight there.
    /// </summary>
    private Session? OpenClassicMenu(Session explorer, bool extendedVerbs)
    {
        TryEnsureExplorerForeground(explorer);
        Step($"Opening the classic Explorer context menu (extended verbs: {extendedVerbs})");

        // Keep any Shift hold short: Windows pops its Filter Keys prompt after eight seconds.
        if (extendedVerbs)
        {
            KeyboardHelper.PressKey(Key.LShift);

            // Injected key input is queued behind posted messages, so give Explorer a moment to see
            // Shift before the context-menu request makes it build the menu.
            Thread.Sleep(300);
        }

        try
        {
            return ShellMenu.OpenForFocusedControl(
                explorer,
                useClassicMenu: true,
                timeoutMS: extendedVerbs ? 7_000 : MenuSurfaceTimeoutMS);
        }
        finally
        {
            if (extendedVerbs)
            {
                KeyboardHelper.ReleaseKey(Key.LShift);
            }
        }
    }

    // ---- Explorer --------------------------------------------------------------------------------

    private void PrepareContextMenuTest()
    {
        contextMenuTest = true;
        Assert.IsTrue(ExplorerControl.CloseFileWindows(), "Stale Explorer file windows could not be closed before the test.");
    }

    private Session OpenExplorer(string folderPath, bool forceHandlerRefresh = false)
    {
        EnsureContextMenuHandlersLoaded(forceHandlerRefresh);
        ExplorerControl.CloseFileWindows();

        Step($"Opening Explorer at '{folderPath}'");
        var explorer = ExplorerControl.OpenFolder(folderPath, timeoutMS: ExplorerTimeoutMS);
        Assert.IsNotNull(explorer, $"Explorer did not open '{folderPath}'.");

        TryEnsureExplorerForeground(explorer!);
        return explorer!;
    }

    /// <summary>
    /// Both handlers register when the module is enabled — the classic registry-COM one always, plus
    /// the sparse MSIX package on signed builds. An Explorer that was already running only picks them
    /// up after the shell restarts, so restart it once per class.
    /// </summary>
    private static void EnsureContextMenuHandlersLoaded(bool force)
    {
        if (explorerRefreshedForRegistration && !force)
        {
            return;
        }

        explorerRefreshedForRegistration = false;
        Thread.Sleep(3_000);
        Assert.IsTrue(
            ExplorerControl.RestartShell(timeoutMS: ExplorerTimeoutMS),
            "Explorer did not expose a taskbar in a fresh process after registering the PowerRename context-menu handlers.");
        explorerRefreshedForRegistration = true;
        Thread.Sleep(2_000);
    }

    private static Session? TrySelectStable(Session explorer, string[] paths)
    {
        if (ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(explorer.WindowHandle), paths, paths[0], timeoutMS: 12_000, requiredConsecutiveMatches: 4).Succeeded)
        {
            return explorer;
        }

        var replacement = ExplorerControl.FindReplacementWindow(explorer, Path.GetDirectoryName(paths[0])!);
        if (replacement is not null &&
            ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(replacement.WindowHandle), paths, paths[0], timeoutMS: 12_000, requiredConsecutiveMatches: 4).Succeeded)
        {
            return replacement;
        }

        return null;
    }

    private void TryEnsureExplorerForeground(Session explorer)
    {
        if (!WindowControl.WaitForForeground(new IntPtr(explorer.WindowHandle), ExplorerTimeoutMS, requiredConsecutiveMatches: 3))
        {
            Step(
                $"Explorer HWND {explorer.WindowHandle} did not become stable foreground; continuing. " +
                $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }
    }
}
