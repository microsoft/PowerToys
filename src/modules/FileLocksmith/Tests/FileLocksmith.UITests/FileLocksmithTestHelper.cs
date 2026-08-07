// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>
/// Names File Locksmith exposes to the outside world: its module key, its UI process/window, and the
/// caption both shell extensions register into the Explorer context menu.
/// </summary>
internal static class FileLocksmithConstants
{
    public const string ModuleName = "File Locksmith";
    public const string PowerRenameModuleName = "PowerRename";
    public const string UiProcessName = "PowerToys.FileLocksmithUI";
    public const string UiExecutableName = "PowerToys.FileLocksmithUI.exe";
    public const string ContextMenuCaption = "Unlock with File Locksmith";

    /// <summary>Sibling PowerToys command used to prove the menu itself still renders.</summary>
    public const string PowerRenameContextMenuCaption = "PowerRename";

    public const string WindowTitle = "File Locksmith";
    public const string ElevatedWindowTitle = "Administrator: File Locksmith";

    public const string ProcessListAutomationId = "ProcessesListView";
    public const string ReloadAutomationId = "ReloadBtn";
    public const string RestartAsAdminAutomationId = "RestartAsAdminBtn";
    public const string EndTaskCaption = "End task";
    public const string EmptyListCaption = "No results";
}

/// <summary>
/// A data file plus a set of uniquely named processes each holding an open handle to it. File
/// Locksmith reports one row per holder — the same shape the release checklist gets from the
/// PowerToys installer's two processes, but with a process name no other process can collide with.
/// </summary>
internal sealed class LockingProcessFixture : IDisposable
{
    /// <summary>Copy of <c>powershell.exe</c>: it can be told to hold a handle and keeps a unique name.</summary>
    public const string LockerFileName = "PTFileLocksmithLocker.exe";

    /// <summary>The file handed to File Locksmith.</summary>
    public const string TargetFileName = "locked-file.dat";

    private static readonly string LockerProcessName = Path.GetFileNameWithoutExtension(LockerFileName);
    private static readonly string LockerSourcePath = Path.Combine(
        Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private readonly List<Process> processes = new();

    /// <param name="targetSubFolder">
    /// Places the locked file in a sub-folder of <see cref="RootFolder"/> so a scan of the root only
    /// finds it when the scan really is recursive.
    /// </param>
    public LockingProcessFixture(string? targetSubFolder = null)
    {
        // File Locksmith matches paths by their kernel name and never resolves 8.3 aliases, so a
        // short path (Path.GetTempPath() returns one whenever the profile name exceeds 8 characters)
        // silently matches nothing. Measured: 0/3 detections short vs 3/3 expanded.
        RootFolder = Path.Combine(
            GetLongPathName(Path.GetTempPath()),
            "PowerToys-FileLocksmith-UITests",
            Guid.NewGuid().ToString("N"));

        var targetFolder = targetSubFolder is null ? RootFolder : Path.Combine(RootFolder, targetSubFolder);
        Directory.CreateDirectory(targetFolder);

        LockerPath = Path.Combine(targetFolder, LockerFileName);
        File.Copy(LockerSourcePath, LockerPath, overwrite: true);

        TargetPath = Path.Combine(targetFolder, TargetFileName);
        File.WriteAllText(TargetPath, "PowerToys File Locksmith UI test fixture.");

        Assert.IsTrue(
            File.Exists(LockerPath) && File.Exists(TargetPath),
            $"The locking fixture was not written to disk under '{targetFolder}'.");
    }

    /// <summary>Temp tree that owns the fixture; scanning it must find the holders recursively.</summary>
    public string RootFolder { get; }

    /// <summary>Full path of the file the started processes hold open.</summary>
    public string TargetPath { get; }

    /// <summary>Full path of the uniquely named executable the holders run.</summary>
    public string LockerPath { get; }

    /// <summary>Folder that directly contains <see cref="TargetPath"/>.</summary>
    public string TargetFolder => Path.GetDirectoryName(TargetPath)!;

    private string HolderErrorLogPath => Path.Combine(RootFolder, "holder-error.log");

    /// <summary>Volume root the fixture lives on, e.g. <c>C:\</c>.</summary>
    public string DriveRoot => Path.GetPathRoot(Path.GetFullPath(RootFolder))!;

    public int RunningCount => processes.Count(process => !HasExited(process));

    /// <summary>
    /// Start <paramref name="count"/> more holders at medium integrity and wait until every one is
    /// alive. File Locksmith launched from the context menu always runs non-elevated
    /// (<c>RunNonElevatedEx</c>) and cannot inspect a higher-integrity process, so the fixture must
    /// stay medium-IL even when the test host is elevated.
    /// </summary>
    public void Start(int count = 1)
    {
        // Expect the holders alive now plus the new ones: a test that killed a holder earlier must
        // not be held to the total ever started.
        var expectedAlive = RunningCount + count;

        for (var index = 0; index < count; index++)
        {
            processes.Add(FileLocksmithUi.HostIsElevated ? StartViaShell() : StartAsChild());
        }

        Assert.IsTrue(
            WaitForRunningCount(expectedAlive, timeoutMS: 20_000),
            $"Only {RunningCount} of {expectedAlive} locking processes stayed alive.{HolderDiagnostics()}");

        // A holder that started is not yet a holder that locked, and one holder locking is not all of
        // them: require a ready marker per holder so a fixture shortfall is never reported as a File
        // Locksmith failure.
        Assert.IsTrue(
            WaitForHoldersReady(expectedAlive, timeoutMS: 30_000),
            $"Only {ReadyHolderCount} of {expectedAlive} locking processes opened " +
            $"'{TargetPath}'.{HolderDiagnostics()}");
    }

    /// <summary>
    /// Start one holder that inherits the elevated test host's token. Only prompt-free (and only
    /// meaningful) when the host is already elevated.
    /// </summary>
    public Process StartElevated()
    {
        Assert.IsTrue(FileLocksmithUi.HostIsElevated, "An elevated locking process needs an elevated test host.");
        var expectedAlive = RunningCount + 1;
        var process = StartAsChild();
        processes.Add(process);
        Assert.IsTrue(
            WaitForRunningCount(expectedAlive, timeoutMS: 20_000) &&
            WaitForHoldersReady(expectedAlive, timeoutMS: 30_000),
            $"The elevated locking process did not open '{TargetPath}'.{HolderDiagnostics()}");
        return process;
    }

    /// <summary>Terminate the oldest live instance without going through the File Locksmith UI.</summary>
    public void KillOne()
    {
        var process = processes.FirstOrDefault(candidate => !HasExited(candidate));
        Assert.IsNotNull(process, "No locking process was alive to terminate.");
        TryKill(process!);
    }

    public bool WaitForRunningCount(int expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            if (RunningCount == expected)
            {
                return true;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        return RunningCount == expected;
    }

    public void Dispose()
    {
        foreach (var process in processes)
        {
            TryKill(process);
            process.Dispose();
        }

        processes.Clear();
        TryDeleteRoot();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathNameW(string lpszShortPath, System.Text.StringBuilder lpszLongPath, uint cchBuffer);

    private static string GetLongPathName(string path)
    {
        var buffer = new StringBuilder(short.MaxValue);
        return GetLongPathNameW(path, buffer, (uint)buffer.Capacity) > 0 ? buffer.ToString() : path;
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch
        {
            // The fixture may already be gone — that's exactly what several tests assert.
        }
    }

    /// <summary>
    /// FileShare.Read (not None) so every holder really keeps its own handle: an exclusive open would
    /// let only the first process hold the file and File Locksmith would correctly report one row.
    /// A holder marks itself ready only after the open succeeds, and records why it could not.
    /// </summary>
    private string BuildHolderCommand() =>
        "try { $handle = [IO.File]::Open('" + TargetPath + "', 'Open', 'Read', 'Read') } " +
        "catch { $_.Exception.ToString() | Set-Content '" + HolderErrorLogPath + "'; exit 1 } " +
        "New-Item -ItemType File -Force -Path ('" + RootFolder + "\\ready-' + $PID + '.marker') | Out-Null; " +
        "Start-Sleep -Seconds 900";

    private int ReadyHolderCount => processes.Count(process =>
        !HasExited(process) && File.Exists(Path.Combine(RootFolder, $"ready-{process.Id}.marker")));

    private bool WaitForHoldersReady(int expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        do
        {
            if (ReadyHolderCount >= expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return ReadyHolderCount >= expected;
    }

    private string HolderDiagnostics()
    {
        try
        {
            if (File.Exists(HolderErrorLogPath))
            {
                return $" Holder error: {File.ReadAllText(HolderErrorLogPath).Trim()}";
            }
        }
        catch
        {
            // Diagnostics must never mask the assertion being reported.
        }

        return string.Empty;
    }

    private Process StartAsChild()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = LockerPath,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", BuildHolderCommand() },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        Assert.IsNotNull(process, $"The locking-process fixture '{LockerPath}' could not be started.");
        return process!;
    }

    /// <summary>
    /// Hand the launch to the (medium-integrity) shell so an elevated test host does not pass its own
    /// token down. Explorer takes no arguments, so a generated VBScript starts the holder hidden from
    /// creation; unlike a temporary .cmd console, it cannot steal foreground from the next Explorer.
    /// </summary>
    private Process StartViaShell()
    {
        var knownProcessIds = Process.GetProcessesByName(LockerProcessName)
            .Select(process =>
            {
                var id = process.Id;
                process.Dispose();
                return id;
            })
            .ToHashSet();

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildHolderCommand()));
        var escapedLockerPath = LockerPath.Replace("\"", "\"\"");
        var launcher = Path.Combine(RootFolder, $"start-{Guid.NewGuid():N}.vbs");
        var launcherCommand =
            $"CreateObject(\"WScript.Shell\").Run \"\"\"{escapedLockerPath}\"\" -NoProfile " +
            $"-NonInteractive -WindowStyle Hidden -EncodedCommand {encodedCommand}\", 0, False{Environment.NewLine}";
        File.WriteAllText(launcher, launcherCommand);

        using var shellLaunch = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{launcher}\"",
            UseShellExecute = true,
        });

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        do
        {
            var started = Process.GetProcessesByName(LockerProcessName)
                .FirstOrDefault(process => !knownProcessIds.Contains(process.Id));
            if (started is not null)
            {
                return started;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail($"The shell did not start the locking-process fixture '{LockerPath}'.");
        return null!;
    }

    private void TryDeleteRoot()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(RootFolder))
                {
                    Directory.Delete(RootFolder, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            Thread.Sleep(250);
        }
    }
}

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
        Assert.IsNotNull(
            resolved,
            $"'{FileLocksmithConstants.UiExecutableName}' was not found. Looked in:{Environment.NewLine}" +
            string.Join(Environment.NewLine, candidates.Distinct()));
        return resolved!;
    }
}
