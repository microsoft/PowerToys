// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

/// <summary>
/// Restarts running instances of a specified process using the Windows Restart Manager.
/// </summary>
internal static class ProcessRestartHelper
{
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Restarts all instances of the specified process name.
    /// </summary>
    /// <param name="processName">The name of the process to restart (without .exe extension).</param>
    /// <param name="shutdownTimeout">Optional timeout for shutdown operation. Default is 30 seconds.</param>
    /// <returns>True if processes were restarted successfully; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when processName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Restart Manager operations fail.</exception>
    internal static async Task<bool> RestartAsync(
        string processName,
        TimeSpan? shutdownTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var timeout = shutdownTimeout ?? DefaultShutdownTimeout;
        var sessionHandle = nint.Zero;

        try
        {
            var processes = GetProcesses(processName);
            if (processes.Length == 0)
            {
                return false;
            }

            sessionHandle = StartRestartManagerSession();

            RegisterResources(sessionHandle, processes);

            await ShutdownOrKillAsync(sessionHandle, processes, timeout);

            RestartSession(sessionHandle);

            return true;
        }
        catch (Exception ex) when (ex is not ArgumentNullException)
        {
            ExtensionHost.LogMessage($"Critical failure: {ex.Message}");
            return false;
        }
        finally
        {
            EndRestartManagerSession(sessionHandle);
        }
    }

    private static nint StartRestartManagerSession()
    {
        var sessionKey = $"PT_{Guid.NewGuid()}";
        var result = NativeMethods.RmStartSession(out var handle, 0, sessionKey);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to start Restart Manager session. Error code: {result}");
        }

        return handle;
    }

    private static void RegisterResources(nint sessionHandle, RM_UNIQUE_PROCESS[] processes)
    {
        var result = NativeMethods.RmRegisterResources(
            sessionHandle,
            0,
            null,
            (uint)processes.Length,
            processes,
            0,
            null);

        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to register resources with Restart Manager. Error code: {result}");
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

    private static void RestartSession(nint sessionHandle)
    {
        var result = NativeMethods.RmRestart(sessionHandle, 0);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to restart processes. Error code: {result}");
        }
    }

    private static void EndRestartManagerSession(nint sessionHandle)
    {
        if (sessionHandle == 0)
        {
            return;
        }

        try
        {
            _ = NativeMethods.RmEndSession(sessionHandle);
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }

    private static RM_UNIQUE_PROCESS[] GetProcesses(string processName)
    {
        var currentSessionId = Process.GetCurrentProcess().SessionId;

        return Process.GetProcessesByName(processName)
            .Select(process => GetProcessInfoSafe(process, currentSessionId))
            .Where(static processInfo => processInfo != null)
            .Select(static processInfo => processInfo!.Value)
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

        static FILETIME ToFileTimeStruct(DateTime dt)
        {
            var ft = dt.ToUniversalTime().ToFileTimeUtc();
            return new FILETIME
            {
                DateTimeLow = (uint)(ft & 0xFFFFFFFF),
                DateTimeHigh = (uint)(ft >> 32),
            };
        }
    }

    private static void KillProcesses(RM_UNIQUE_PROCESS[] processes)
    {
        foreach (var proc in processes)
        {
            try
            {
                Process.GetProcessById(proc.ProcessId).Kill();
            }
            catch (ArgumentException)
            {
                // Process might have exited already
            }
            catch (Exception ex)
            {
                ExtensionHost.LogMessage(
                    $"Failed to kill process ID {proc.ProcessId}: {ex.Message}");
            }
        }
    }
}
