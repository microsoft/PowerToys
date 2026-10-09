// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    private const int ExplorerTimeoutMS = 30_000;
    private const int MenuSurfaceTimeoutMS = 25_000;

    private static bool shellRestarted;

    public static bool IsWindows11OrNewer => Environment.OSVersion.Version.Build >= 22_000;

    public static Session OpenFolder(string folderPath) => OpenLocation(folderPath, folderPath);

    /// <summary>Open "This PC", the only view that exposes drive roots as selectable Shell items.</summary>
    public static Session OpenThisPc() => OpenLocation("shell:MyComputerFolder", "This PC");

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

        Thread.Sleep(3_000);
        Assert.IsTrue(
            ExplorerControl.RestartShell(timeoutMS: ExplorerTimeoutMS),
            "Explorer did not expose a taskbar in a fresh process after registering the context-menu handlers.");
        shellRestarted = true;
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
                command = ShellMenu.FindVisibleMenuItem(menu, commandCaption, timeoutMS: 5_000);
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

        var replacement = ExplorerControl.FindReplacementWindow(explorer);
        if (replacement is not null &&
            ExplorerShell.SetSelectionAndWaitForStable(
                new IntPtr(replacement.WindowHandle), paths, paths[0], timeoutMS, requiredConsecutiveMatches: 4).Succeeded)
        {
            return replacement;
        }

        return null;
    }

    private static Session OpenLocation(string folderPath, string diagnosticName)
    {
        EnsureShellRestartedOnce();
        ExplorerControl.CloseFileWindows();

        var explorer = ExplorerControl.OpenFolder(folderPath, timeoutMS: ExplorerTimeoutMS);
        Assert.IsNotNull(explorer, $"Explorer did not open '{diagnosticName}'.");

        EnsureForeground(explorer!);
        return explorer!;
    }

    private static Session? OpenMenu(Session explorer, ContextMenuTier tier)
    {
        EnsureForeground(explorer);
        KeyboardHelper.SendKeys(Key.Esc);

        var menu = ShellMenu.OpenForFocusedControl(
            explorer,
            useClassicMenu: tier == ContextMenuTier.Classic,
            timeoutMS: MenuSurfaceTimeoutMS);
        var expectModern = tier == ContextMenuTier.Default && IsWindows11OrNewer;
        return menu is not null && WindowsFinder.ListByApp(ExplorerControl.ProcessName).Any(window =>
            window.Hwnd == menu.WindowHandle &&
            (expectModern ? ShellMenu.IsModernWindow(window) : ShellMenu.IsClassicWindow(window)))
            ? menu
            : null;
    }

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
                ShellMenu.FindVisibleMenuItem(menu, commandCaption, timeoutMS: 250) is not null,
                siblingCaption is null || ShellMenu.FindVisibleMenuItem(menu, siblingCaption, timeoutMS: 250) is not null);
        }
        catch (Exception)
        {
            // winappcli reports the popup's HWND as gone mid-query; treat it as not-yet-stable.
            return new MenuObservation(false, false, false);
        }
    }

    private static void EnsureForeground(Session explorer) => Assert.IsTrue(
        WindowControl.WaitForForeground(
            new IntPtr(explorer.WindowHandle),
            ExplorerTimeoutMS,
            requiredConsecutiveMatches: 3),
        $"Explorer HWND {explorer.WindowHandle} was not the stable foreground window. " +
        $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
}
