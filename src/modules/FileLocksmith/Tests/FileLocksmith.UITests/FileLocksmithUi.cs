// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>
/// Drives the File Locksmith window: writes the same paths file the shell extensions write, starts
/// <c>PowerToys.FileLocksmithUI.exe</c>, and reads/acts on the process list.
/// </summary>
/// <remarks>
/// Launching the UI directly reproduces the product's own IPC contract
/// (<c>%LocalAppData%\Microsoft\PowerToys\File Locksmith\last-run.log</c>, UTF-16 paths terminated by
/// a blank line — see <c>FileLocksmithLib/IPC.cpp</c> and
/// <c>FileLocksmithLibInterop/NativeMethods.cpp</c>). It keeps the list-behaviour tests independent
/// of Explorer; <see cref="FileLocksmithContextMenuTests"/> covers the context-menu surface itself.
/// </remarks>
internal static class FileLocksmithUi
{
    private const int LaunchTimeoutMS = 30_000;

    private static readonly Lazy<string> ExecutablePathValue = new(ResolveExecutablePath);

    /// <summary>True when the test host is elevated, which every child process it starts inherits.</summary>
    public static bool HostIsElevated { get; } = ElevationHelper.IsCurrentProcessElevated();

    /// <summary>Resolved path of <c>PowerToys.FileLocksmithUI.exe</c> in the build under test.</summary>
    public static string ExecutablePath => ExecutablePathValue.Value;

    public static string PathsFilePath => Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        FileLocksmithConstants.ModuleName,
        "last-run.log");

    /// <summary>Start File Locksmith on <paramref name="paths"/> and wait until its list has loaded.</summary>
    public static Session Launch(params string[] paths) => Launch(LaunchTimeoutMS, elevated: false, paths);

    /// <summary>Start an elevated File Locksmith. Only prompt-free when the test host is elevated.</summary>
    public static Session LaunchElevated(params string[] paths) => Launch(LaunchTimeoutMS, elevated: true, paths);

    public static Session Launch(int loadTimeoutMS, bool elevated, params string[] paths)
    {
        Assert.IsTrue(paths.Length > 0, "At least one path must be handed to File Locksmith.");
        Close();
        WritePathsFile(paths);
        StartProcess(elevated);
        return WaitForWindow(loadTimeoutMS);
    }

    /// <summary>Bind to the File Locksmith window and block until its process list finished loading.</summary>
    public static Session WaitForWindow(int loadTimeoutMS)
    {
        var window = WindowsFinder.WaitForWindowByApp(
            FileLocksmithConstants.UiProcessName,
            candidate => candidate.Width > 0 && candidate.Height > 0,
            timeoutMS: LaunchTimeoutMS);
        Assert.IsNotNull(window, "The File Locksmith window did not open.");
        var foregroundReady = WaitHelper.WaitForStable(
            observe: WindowControl.GetForegroundWindowInfo,
            isMatch: foreground => foreground.ProcessId == window!.ProcessId,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            recover: _ => WindowControl.TryFocusByApp(
                window!.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))).Succeeded;
        if (!foregroundReady)
        {
            Console.WriteLine(
                $"File Locksmith foreground could not be confirmed; continuing with UIA. " +
                $"Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }

        Assert.IsTrue(
            WaitForLoaded(window, loadTimeoutMS),
            $"File Locksmith was still scanning after {loadTimeoutMS}ms — its process list never appeared.");
        return window;
    }

    /// <summary>
    /// The list only enters the UIA tree once <c>IsLoading</c> flips back to false, so its presence is
    /// the authoritative "scan finished" signal.
    /// </summary>
    public static bool WaitForLoaded(Session ui, int timeoutMS) => ui.WaitFor(
        () => ui.Has(By.AccessibilityId(FileLocksmithConstants.ProcessListAutomationId), timeoutMS: 1_000),
        timeoutMS: timeoutMS,
        pollIntervalMS: 500);

    /// <summary>
    /// The "End task" label of every listed row, top row first. The label is matched, not its Button:
    /// the button wraps an icon+text panel and exposes no UIA name of its own, so a Button-typed search
    /// finds nothing. Clicking the label lands inside the button.
    /// </summary>
    public static IReadOnlyList<TextBlock> EndTaskLabels(Session ui, int timeoutMS = 5_000) =>
        ui.FindAll<TextBlock>(By.Name(FileLocksmithConstants.EndTaskCaption), timeoutMS)
            .Where(label =>
                label.Name.Equals(FileLocksmithConstants.EndTaskCaption, StringComparison.OrdinalIgnoreCase) &&
                label.Width > 0 &&
                label.Height > 0)
            .OrderBy(label => label.Y)
            .ToList();

    /// <summary>
    /// Number of listed rows, counted by their End task labels - one per row, and independent of the
    /// process name, which the paths header also displays.
    /// </summary>
    public static int CountRows(Session ui, int timeoutMS = 5_000) => EndTaskLabels(ui, timeoutMS).Count;

    /// <summary>True when at least one row is headed by <paramref name="processName"/>.</summary>
    public static bool HasProcessRow(Session ui, string processName, int timeoutMS = 5_000) =>
        ui.FindAll<TextBlock>(By.Name(processName), timeoutMS)
            .Any(row => row.Name.Equals(processName, StringComparison.OrdinalIgnoreCase));

    public static bool WaitForRowCount(Session ui, int expected, int timeoutMS) =>
        ui.WaitFor(
            () => CountRows(ui, timeoutMS: expected == 0 ? 500 : 2_000) == expected,
            timeoutMS: timeoutMS,
            pollIntervalMS: 500);

    /// <summary>
    /// Wait for <paramref name="expected"/> rows, re-scanning through Reload between attempts. The
    /// window scans once when it opens, so a scan that came up short can only be retried the way a
    /// user would - by pressing Reload.
    /// </summary>
    public static bool WaitForRowCountWithReload(Session ui, int expected, int timeoutMS, int reloadAttempts = 3)
    {
        var perAttempt = Math.Max(timeoutMS / (reloadAttempts + 1), 3_000);
        for (var attempt = 0; ; attempt++)
        {
            if (WaitForRowCount(ui, expected, perAttempt))
            {
                return true;
            }

            if (attempt >= reloadAttempts)
            {
                return false;
            }

            ClickReload(ui);
            WaitForLoaded(ui, timeoutMS: 30_000);
        }
    }

    /// <summary>Press the toolbar Reload (refresh) button and let the rescan start.</summary>
    public static void ClickReload(Session ui) =>
        ui.Find<Button>(By.AccessibilityId(FileLocksmithConstants.ReloadAutomationId), timeoutMS: 10_000)
            .Click(msPostAction: 300);

    public static bool HasRestartAsAdminButton(Session ui, int timeoutMS = 3_000) =>
        ui.Has(By.AccessibilityId(FileLocksmithConstants.RestartAsAdminAutomationId), timeoutMS);

    /// <summary>Live window title, re-read from Win32 so an elevated relaunch is observed.</summary>
    public static string? CurrentWindowTitle() =>
        WindowsFinder.ListByApp(FileLocksmithConstants.UiProcessName)
            .FirstOrDefault(window => window.Width > 0 && window.Height > 0)?
            .Title;

    public static bool Close()
    {
        if (WindowControl.TryCloseByApp(FileLocksmithConstants.UiProcessName, timeoutMS: 5_000) &&
            WaitForProcess(FileLocksmithConstants.UiProcessName, expected: false, timeoutMS: 2_000))
        {
            return true;
        }

        return WindowControl.TryKillProcessTreeByNameAndWait(FileLocksmithConstants.UiProcessName, timeoutMS: 10_000);
    }

    public static bool WaitForProcess(string processName, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            var processes = Process.GetProcessesByName(processName);
            var running = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            if (running == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    /// <summary>
    /// Write the UTF-16 paths file exactly as <c>ipc::Writer</c> does: every path followed by a wide
    /// newline, then one more newline as the terminator the reader stops on.
    /// </summary>
    private static void WritePathsFile(IReadOnlyList<string> paths)
    {
        var builder = new StringBuilder();
        foreach (var path in paths)
        {
            builder.Append(path).Append('\n');
        }

        builder.Append('\n');

        Directory.CreateDirectory(Path.GetDirectoryName(PathsFilePath)!);
        File.WriteAllBytes(PathsFilePath, Encoding.Unicode.GetBytes(builder.ToString()));
    }

    private static void StartProcess(bool elevated)
    {
        var executable = ExecutablePathValue.Value;
        var workingDirectory = Path.GetDirectoryName(executable)!;

        if (elevated)
        {
            using var elevatedLaunch = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                Verb = "runas",
            });
            return;
        }

        if (!HostIsElevated)
        {
            using var directLaunch = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            });
            return;
        }

        // An elevated test host would hand its own token to a direct child, and File Locksmith
        // behaves differently when elevated. Hand the launch to the (medium-integrity) shell instead,
        // which is what the context-menu extension's RunNonElevatedEx does.
        using var shellLaunch = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{executable}\"",
            UseShellExecute = true,
        });
    }

    private static string ResolveExecutablePath()
    {
        var candidates = new List<string>();

        var overrideDirectory = Environment.GetEnvironmentVariable("POWERTOYS_INSTALL_DIR");
        if (!string.IsNullOrEmpty(overrideDirectory))
        {
            candidates.Add(Path.Combine(overrideDirectory, "WinUI3Apps", FileLocksmithConstants.UiExecutableName));
        }

        // The build output that holds WinUI3Apps is an ancestor of the test assembly, both locally
        // (<root>\<plat>\<cfg>\tests\<proj>\<tfm>\) and in CI (the downloaded build artifact).
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "WinUI3Apps", FileLocksmithConstants.UiExecutableName));
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerToys",
            "WinUI3Apps",
            FileLocksmithConstants.UiExecutableName));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerToys",
            "WinUI3Apps",
            FileLocksmithConstants.UiExecutableName));

        var resolved = candidates.FirstOrDefault(File.Exists);
        string notFoundMessage =
            $"'{FileLocksmithConstants.UiExecutableName}' was not found. Looked in:{Environment.NewLine}" +
            string.Join(Environment.NewLine, candidates.Distinct());
        Assert.IsNotNull(resolved, notFoundMessage);
        return resolved!;
    }
}
