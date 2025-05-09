// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

/// <summary>
/// Restarts running instances of system shell (Windows Explorer).
/// </summary>
internal static class ShellRestartHelper
{
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PostRestartCheckDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Restarts all instances of the "explorer.exe" process in the current session.
    /// </summary>
    internal static async Task RestartAllInCurrentSession()
    {
        // Restarting Windows Explorer:
        // - Explorer can have multiple processes running in the same session. Let's not speculate why the user
        //   wants to restart it and terminate them all.
        // - Explorer should always run un-elevated. If started elevated, it restarts itself (CreateExplorerShellUnelevatedTask).
        //   That means we don't have to worry about elevated processes.
        // - Restart Manager will restore opened folder windows after restart (only if enabled in Folder Options).
        // - Restarting by will make the new explorer.exe process a child process of CmdPal. This is not much of a
        //   problem unless something kills the entire CmdPal process tree.
        await RestartProcessesInCurrentSessionAsync("explorer.exe");

        // - Windows can automatically restart the shell if it detects that it has crashed. This can be disabled
        //   in registry (key AutoRestartShell).
        // - Restart Manager is not guaranteed to restart all the processes it closes.
        await EnsureProcessIsRunning("explorer.exe");
    }

    /// <summary>
    /// Restarts all instances of the specified process name.
    /// </summary>
    /// <param name="processExecutableName">The name of the process to restart.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when processName is null.</exception>
    /// <exception cref="ArgumentException">Thrown when processName is null or consists only of white-space characters.</exception>
    private static async Task RestartProcessesInCurrentSessionAsync(string processExecutableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processExecutableName);

        var restartManagerSessionHandle = nint.Zero;

        try
        {
            var processName = Path.GetFileNameWithoutExtension(processExecutableName);
            var uniqueProcesses = GetProcesses(processName);
            if (uniqueProcesses.Length == 0)
            {
                return;
            }

            var result = NativeMethods.RmStartSession(out restartManagerSessionHandle, 0, $"PT_{Guid.NewGuid()}");
            ThrowIfError(result, "start Restart Manager session");

            result = NativeMethods.RmRegisterResources(restartManagerSessionHandle, 0, null, (uint)uniqueProcesses.Length, uniqueProcesses, 0, null);
            ThrowIfError(result, "register resources with Restart Manager");

            await ShutdownOrKillAsync(restartManagerSessionHandle, uniqueProcesses, DefaultShutdownTimeout);

            result = NativeMethods.RmRestart(restartManagerSessionHandle, 0);
            ThrowIfError(result, "restart processes");
        }
        catch (Exception ex) when (ex is not ArgumentNullException)
        {
            ExtensionHost.LogMessage($"Critical failure: {ex.Message}");
        }
        finally
        {
            if (restartManagerSessionHandle != 0)
            {
                try
                {
                    _ = NativeMethods.RmEndSession(restartManagerSessionHandle);
                }
                catch
                {
                    // Suppress cleanup exceptions
                }
            }
        }
    }

    private static async Task ShutdownOrKillAsync(
        nint sessionHandle,
        RM_UNIQUE_PROCESS[] processes,
        TimeSpan timeout)
    {
        // RmShutdown is a blocking call that can take a long time to return. We can run it in a separate thread
        // and kill the processes when we run out of patience.
        // https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmshutdown
        var shutdownTask = Task.Run(() => NativeMethods.RmShutdown(sessionHandle, RM_SHUTDOWN_TYPE.RmForceShutdown));

        try
        {
            var result = await shutdownTask.WaitAsync(timeout);
            if (result != 0)
            {
                ExtensionHost.LogMessage($"RmShutdown returned error {result}, performing force kill.");
                KillProcesses(processes);
            }
        }
        catch (TimeoutException)
        {
            KillProcesses(processes);
            try
            {
                await shutdownTask;
            }
            catch
            {
                // ignore
            }
        }
    }

    private static RM_UNIQUE_PROCESS[] GetProcesses(string processName)
    {
        var currentSessionId = Process.GetCurrentProcess().SessionId;

        return Process.GetProcessesByName(processName)
            .Select(process => GetProcessInfoSafe(process, currentSessionId))
            .OfType<RM_UNIQUE_PROCESS>()
            .ToArray();

        static RM_UNIQUE_PROCESS? GetProcessInfoSafe(Process process, int targetSessionId)
        {
            try
            {
                if (process.HasExited || process.SessionId != targetSessionId)
                {
                    return null;
                }

                return new RM_UNIQUE_PROCESS
                {
                    ProcessId = process.Id,
                    ProcessStartTime = ToFileTimeStruct(process.StartTime),
                };
            }
            catch (Exception ex)
            {
                ExtensionHost.LogMessage($"Error retrieving processes (Process ID {process.Id}): {ex.Message}");
                return null;
            }
        }

        static FILETIME ToFileTimeStruct(DateTime dateTime)
        {
            var fileTime = dateTime.ToFileTimeUtc();
            return new FILETIME
            {
                DateTimeLow = (uint)fileTime,
                DateTimeHigh = (uint)(fileTime >> 32),
            };
        }
    }

    private static void KillProcesses(RM_UNIQUE_PROCESS[] processes)
    {
        foreach (var process in processes)
        {
            try
            {
                Process.GetProcessById(process.ProcessId).Kill();
            }
            catch (ArgumentException)
            {
                // Process might have exited already
            }
            catch (Exception ex)
            {
                ExtensionHost.LogMessage($"Failed to kill process ID {process.ProcessId}: {ex.Message}");
            }
        }
    }

    private static void ThrowIfError(uint result, string operation)
    {
        if ((SystemErrorCode)result != SystemErrorCode.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Failed to {operation}. Error code: {result}");
        }
    }

    /// <summary>
    /// Ensures that the specified process is running. If the process is not running, it attempts to start it.
    /// </summary>
    /// <param name="processExecutableName">The name of the process to restart.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task EnsureProcessIsRunning(string processExecutableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processExecutableName);

        var processName = Path.GetFileNameWithoutExtension(processExecutableName);

        if (GetProcesses(processName).Length > 0)
        {
            return;
        }

        await Task.Delay(PostRestartCheckDelay);

        if (GetProcesses(processName).Length > 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(processExecutableName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Fail-safe failed to start {processExecutableName}: {ex.Message}");
        }
    }
}
