// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>Which Explorer context-menu surface a probe should drive.</summary>
internal enum ContextMenuTier
{
    /// <summary>Whatever the OS shows on a plain right-click: tier-1 on Windows 11, classic on Windows 10.</summary>
    Default,

    /// <summary>The classic <c>#32768</c> menu, reached through "Show more options" on Windows 11.</summary>
    Classic,
}

/// <summary>One stable look at an open context menu.</summary>
internal sealed record MenuObservation(bool IsOpen, bool HasCommand, bool HasSibling);

/// <summary>
/// Opens Explorer, establishes an exact Shell selection, and drives either context-menu tier. The
/// selection is re-established on every attempt because a slow agent re-renders the view
/// asynchronously after a module toggles or the shell restarts.
/// </summary>
internal static class ExplorerHelper
{
    public const string ClassicMenuClassName = "#32768";
    public const string ModernMenuClassName = "Microsoft.UI.Content.PopupWindowSiteBridge";

    private const string ExplorerProcessName = "explorer";
    private const string ShowMoreOptionsCaption = "Show more options";
    private const int ExplorerTimeoutMS = 30_000;
    private const int MenuSurfaceTimeoutMS = 25_000;

    private static bool shellRestarted;

    public static bool IsWindows11OrNewer => Environment.OSVersion.Version.Build >= 22_000;

    public static Session OpenFolder(string folderPath) => OpenLocation($"/n,\"{folderPath}\"", folderPath);

    /// <summary>Open "This PC", the only view that exposes drive roots as selectable Shell items.</summary>
    public static Session OpenThisPc() => OpenLocation("shell:MyComputerFolder", "This PC");

    public static bool CloseFileWindows() =>
        WindowControl.TryCloseByApp(ExplorerProcessName, IsExplorerFileWindow, timeoutMS: 10_000);

    /// <summary>
    /// Both handlers register at module-enable time — the classic registry-COM one always, the modern
    /// sparse-MSIX package on signed builds. An Explorer that was already running only surfaces them
    /// after the shell restarts, so do it exactly once per test run.
    /// </summary>
    public static void EnsureShellRestartedOnce()
    {
        if (shellRestarted)
        {
            return;
        }

        shellRestarted = true;
        Thread.Sleep(3_000);

        var previousProcessIds = Process.GetProcessesByName(ExplorerProcessName)
            .Select(process =>
            {
                var id = process.Id;
                process.Dispose();
                return id;
            })
            .ToHashSet();

        // Only explorer.exe: Kill(entireProcessTree) would also take down anything launched from it.
        WindowControl.TryKillProcessByName(ExplorerProcessName);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var current = Process.GetProcessesByName(ExplorerProcessName);
            var hasFreshShell = current.Any(process => !previousProcessIds.Contains(process.Id));
            foreach (var process in current)
            {
                process.Dispose();
            }

            if (hasFreshShell)
            {
                break;
            }

            Thread.Sleep(500);
        }

        Thread.Sleep(2_000);
    }

    /// <summary>
    /// Poll the requested menu tier until it reports <paramref name="expectedCommand"/> across
    /// consecutive samples, re-selecting (and if needed reopening) the view on every attempt.
    /// </summary>
    public static (bool Succeeded, MenuObservation Last, Session Explorer) ProbeCommand(
        Session explorer,
        Func<Session> reopenExplorer,
        string[] selection,
        ContextMenuTier tier,
        string commandCaption,
        bool expectedCommand,
        string? siblingCaption,
        TestContext testContext,
        int deadlineSeconds = 120)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(deadlineSeconds);
        var last = new MenuObservation(false, false, false);
        var selectionFailures = 0;

        do
        {
            KeyboardHelper.SendKeys(Key.Esc);

            var selected = TrySelectStable(explorer, selection, timeoutMS: 12_000);
            if (selected is null)
            {
                testContext.WriteLine(
                    $"Explorer selection did not settle for [{string.Join(", ", selection)}]. " +
                    $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
                if (++selectionFailures >= 2)
                {
                    selectionFailures = 0;
                    explorer = reopenExplorer();
                }

                Thread.Sleep(300);
                continue;
            }

            selectionFailures = 0;
            explorer = selected;

            var menu = OpenMenu(explorer, tier);
            if (menu is null)
            {
                Thread.Sleep(300);
                continue;
            }

            var stable = WaitHelper.WaitForStable(
                observe: () => Observe(menu, commandCaption, siblingCaption),
                isMatch: observation => observation is not null &&
                                        observation.IsOpen &&
                                        observation.HasCommand == expectedCommand &&
                                        (siblingCaption is null || observation.HasSibling),
                timeoutMS: 8_000,
                requiredConsecutiveMatches: 4,
                pollIntervalMS: 250);
            last = stable.LastObservation ?? last;
            KeyboardHelper.SendKeys(Key.Esc);

            if (stable.Succeeded)
            {
                return (true, last, explorer);
            }

            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        return (false, last, explorer);
    }

    /// <summary>Open the requested tier and invoke <paramref name="commandCaption"/> on it.</summary>
    public static Session InvokeCommand(
        Session explorer,
        Func<Session> reopenExplorer,
        string[] selection,
        ContextMenuTier tier,
        string commandCaption,
        int deadlineSeconds = 120)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(deadlineSeconds);
        Session? menu = null;
        Element? command = null;
        var selectionFailures = 0;

        do
        {
            var selected = TrySelectStable(explorer, selection, timeoutMS: 12_000);
            if (selected is null)
            {
                if (++selectionFailures >= 2)
                {
                    selectionFailures = 0;
                    explorer = reopenExplorer();
                }

                Thread.Sleep(300);
                continue;
            }

            selectionFailures = 0;
            explorer = selected;

            menu = OpenMenu(explorer, tier);
            if (menu is not null)
            {
                command = FindVisibleMenuItem(menu, commandCaption, timeoutMS: 5_000);
                if (command is not null)
                {
                    break;
                }
            }

            KeyboardHelper.SendKeys(Key.Esc);
            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        Assert.IsNotNull(menu, "Explorer did not open the expected context-menu surface.");
        Assert.IsNotNull(
            command,
            $"Explorer did not show the '{commandCaption}' command for [{string.Join(", ", selection)}].");
        command!.Invoke(msPostAction: 300);
        return explorer;
    }

    /// <summary>
    /// Non-throwing selection: re-establishes an exact, stable Shell selection and returns the live
    /// session, handling an Explorer window that was replaced mid-render.
    /// </summary>
    public static Session? TrySelectStable(Session explorer, string[] paths, int timeoutMS)
    {
        if (ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(explorer.WindowHandle), paths, paths[0], timeoutMS, requiredConsecutiveMatches: 4).Succeeded)
        {
            return explorer;
        }

        var replacement = FindReplacementExplorer(explorer);
        if (replacement is not null &&
            ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(replacement.WindowHandle), paths, paths[0], timeoutMS, requiredConsecutiveMatches: 4).Succeeded)
        {
            return replacement;
        }

        return null;
    }

    public static Element? FindVisibleMenuItem(Session menu, string caption, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            var item = menu.FindAll<Element>(By.Name(caption), timeoutMS: 250)
                .FirstOrDefault(element =>
                    element.Name.Contains(caption, StringComparison.OrdinalIgnoreCase) &&
                    element.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) &&
                    element.Width > 0 &&
                    element.Height > 0);
            if (item is not null)
            {
                return item;
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private static Session OpenLocation(string arguments, string diagnosticName)
    {
        EnsureShellRestartedOnce();
        CloseFileWindows();

        var existingHandles = WindowsFinder.ListByApp(ExplorerProcessName)
            .Where(IsExplorerFileWindow)
            .Select(window => window.Hwnd)
            .ToHashSet();

        using var explorerLaunch = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true,
        });

        var explorer = WindowsFinder.WaitForWindowByApp(
            ExplorerProcessName,
            window => IsExplorerFileWindow(window) && !existingHandles.Contains(window.Hwnd),
            timeoutMS: ExplorerTimeoutMS);
        Assert.IsNotNull(explorer, $"Explorer did not open '{diagnosticName}'.");

        EnsureForeground(explorer!);
        return explorer!;
    }

    private static Session? OpenMenu(Session explorer, ContextMenuTier tier)
    {
        EnsureForeground(explorer);
        KeyboardHelper.SendKeys(Key.Esc);

        if (!WindowControl.TryOpenContextMenuForFocusedControl(new IntPtr(explorer.WindowHandle)))
        {
            return null;
        }

        var firstSurface = WaitForMenuSurface(
            IsWindows11OrNewer ? ModernMenuClassName : ClassicMenuClassName,
            MenuSurfaceTimeoutMS);
        if (firstSurface is null || tier == ContextMenuTier.Default || !IsWindows11OrNewer)
        {
            return firstSurface;
        }

        // Windows 11 only: the classic menu lives one level down, behind "Show more options".
        var showMoreOptions = FindVisibleMenuItem(firstSurface, ShowMoreOptionsCaption, timeoutMS: 5_000);
        if (showMoreOptions is null)
        {
            return null;
        }

        try
        {
            showMoreOptions.Invoke(msPostAction: 300);
        }
        catch (Exception)
        {
            // The popup can vanish between finding and invoking it; let the caller reopen the menu.
            return null;
        }

        return WaitForMenuSurface(ClassicMenuClassName, MenuSurfaceTimeoutMS);
    }

    private static Session? WaitForMenuSurface(string className, int timeoutMS) =>
        WindowsFinder.WaitForWindow(
            window => className == ClassicMenuClassName
                ? window.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)
                : window.ClassName.Contains(className, StringComparison.OrdinalIgnoreCase),
            timeoutMS: timeoutMS,
            pollIntervalMS: 100);

    private static MenuObservation Observe(Session menu, string commandCaption, string? siblingCaption)
    {
        var menuReady = menu.WindowHandle != 0 &&
            WindowsFinder.ListAll().Any(window => window.Hwnd == menu.WindowHandle);
        if (!menuReady)
        {
            return new MenuObservation(false, false, false);
        }

        try
        {
            return new MenuObservation(
                true,
                FindVisibleMenuItem(menu, commandCaption, timeoutMS: 250) is not null,
                siblingCaption is null || FindVisibleMenuItem(menu, siblingCaption, timeoutMS: 250) is not null);
        }
        catch (Exception)
        {
            // winappcli reports the popup's HWND as gone mid-query; treat it as not-yet-stable.
            return new MenuObservation(false, false, false);
        }
    }

    private static Session? FindReplacementExplorer(Session explorer)
    {
        var foregroundWindow = WindowControl.GetForegroundWindowHandle().ToInt64();
        var replacement = WindowsFinder.ListByApp(ExplorerProcessName)
            .Where(IsExplorerFileWindow)
            .Where(window => window.Hwnd != explorer.WindowHandle)
            .OrderByDescending(window => window.Hwnd == foregroundWindow)
            .FirstOrDefault();
        if (replacement is null)
        {
            return null;
        }

        return WindowsFinder.WaitForWindow(
            window => window.Hwnd == replacement.Hwnd,
            timeoutMS: 2_000,
            pollIntervalMS: 100);
    }

    private static void EnsureForeground(Session explorer) => Assert.IsTrue(
        WindowControl.WaitForForeground(
            new IntPtr(explorer.WindowHandle),
            ExplorerTimeoutMS,
            requiredConsecutiveMatches: 3),
        $"Explorer HWND {explorer.WindowHandle} was not the stable foreground window. " +
        $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");

    private static bool IsExplorerFileWindow(WindowsFinder.WindowInfo window) =>
        window.ClassName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase);
}
