// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.FileLocksmith.UITests;

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
        bool holdersReady = WaitForHoldersReady(expectedAlive, timeoutMS: 30_000);
        string holdersMessage =
            $"Only {ReadyHolderCount} of {expectedAlive} locking processes opened " +
            $"'{TargetPath}'.{HolderDiagnostics()}";
        Assert.IsTrue(holdersReady, holdersMessage);
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
