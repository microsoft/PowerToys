// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ManagedCommon
{
    public static class RunnerHelper
    {
        public static void WaitForPowerToysRunner(int powerToysPID, Action act, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            var assembly = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported
                ? Assembly.GetCallingAssembly().GetName()
                : typeof(RunnerHelper).Assembly.GetName();
            WaitForPowerToysRunnerCore(powerToysPID, act, null, memberName, assembly);
        }

        // Keep all four arguments required so existing three-argument calls remain unambiguous.
        public static void WaitForPowerToysRunner(int powerToysPID, Action act, Action<Win32Exception> onFailure, string memberName)
        {
            var assembly = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported
                ? Assembly.GetCallingAssembly().GetName()
                : typeof(RunnerHelper).Assembly.GetName();
            WaitForPowerToysRunnerCore(powerToysPID, act, onFailure, memberName, assembly);
        }

        private static void WaitForPowerToysRunnerCore(int powerToysPID, Action act, Action<Win32Exception> onFailure, string memberName, AssemblyName assembly)
        {
            PowerToysTelemetry.Log.WriteEvent(new DebugEvent() { Message = $"[{assembly}][{memberName}]WaitForPowerToysRunner waiting for Event powerToysPID={powerToysPID}" });
            Task.Run(() =>
            {
                const uint INFINITE = 0xFFFFFFFF;
                const uint WAIT_OBJECT_0 = 0x00000000;
                const uint SYNCHRONIZE = 0x00100000;
                const int ERROR_INVALID_PARAMETER = 87;

                IntPtr powerToysProcHandle = NativeMethods.OpenProcess(SYNCHRONIZE, false, powerToysPID);
                if (powerToysProcHandle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();

                    // OpenProcess reports an already-terminated, valid PID as invalid parameter.
                    if (powerToysPID > 0 && error == ERROR_INVALID_PARAMETER)
                    {
                        PowerToysTelemetry.Log.WriteEvent(new DebugEvent() { Message = $"[{assembly}][{memberName}]WaitForPowerToysRunner Event Notified powerToysPID={powerToysPID}" });
                        act.Invoke();
                    }
                    else
                    {
                        var exception = new Win32Exception(error);
                        Logger.LogError($"Could not open process {powerToysPID} for synchronization.", exception);
                        onFailure?.Invoke(exception);
                    }

                    return;
                }

                uint waitResult;
                int waitError;
                try
                {
                    waitResult = NativeMethods.WaitForSingleObject(powerToysProcHandle, INFINITE);
                    waitError = Marshal.GetLastWin32Error();
                }
                finally
                {
                    NativeMethods.CloseHandle(powerToysProcHandle);
                }

                if (waitResult == WAIT_OBJECT_0)
                {
                    PowerToysTelemetry.Log.WriteEvent(new DebugEvent() { Message = $"[{assembly}][{memberName}]WaitForPowerToysRunner Event Notified powerToysPID={powerToysPID}" });
                    act.Invoke();
                }
                else
                {
                    var exception = new Win32Exception(waitError);
                    Logger.LogError($"Could not wait for process {powerToysPID} (wait result {waitResult}).", exception);
                    onFailure?.Invoke(exception);
                }
            });
        }

        private static readonly string PowerToysRunnerProcessName = "PowerToys.exe";

        // In case we don't have a permission to open user's processes with a SYNCHRONIZE access right, e.g. LocalSystem processes, we could use GetExitCodeProcess to check the process' exit code periodically.
        public static void WaitForPowerToysRunnerExitFallback(Action act)
        {
            int[] processIds = new int[1024];
            uint bytesCopied;

            NativeMethods.EnumProcesses(processIds, (uint)processIds.Length * sizeof(uint), out bytesCopied);

            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            var handleAccess = PROCESS_QUERY_LIMITED_INFORMATION;

            IntPtr runnerHandle = IntPtr.Zero;
            foreach (var processId in processIds)
            {
                IntPtr hProcess = NativeMethods.OpenProcess(handleAccess, false, processId);
                System.Text.StringBuilder name = new System.Text.StringBuilder(1024);
                uint length = 1024;
                if (hProcess == IntPtr.Zero || !NativeMethods.QueryFullProcessImageName(hProcess, 0, name, ref length))
                {
                    continue;
                }

                if (System.IO.Path.GetFileName(name.ToString()) == PowerToysRunnerProcessName)
                {
                    runnerHandle = hProcess;
                    break;
                }
            }

            if (runnerHandle == IntPtr.Zero)
            {
                Logger.LogError("Couldn't determine PowerToys.exe pid");
                return;
            }

            Task.Run(() =>
            {
                const int STILL_ACTIVE = 0x103;
                uint exit_status;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    NativeMethods.GetExitCodeProcess(runnerHandle, out exit_status);
                }
                while (exit_status == STILL_ACTIVE);

                NativeMethods.CloseHandle(runnerHandle);

                act.Invoke();
            });
        }
    }
}
