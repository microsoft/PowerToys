// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using PowerScripts.Client;

namespace PowerScripts.Host;

internal static class HostSession
{
    internal static int Run(Func<string[], int> invoke, int? ownerProcessId = null, long? stopEventHandle = null)
    {
        using var owner = ownerProcessId is { } processId ? Process.GetProcessById(processId) : null;
        if (owner is not null)
        {
            owner.Exited += (_, _) => Stop();
            owner.EnableRaisingEvents = true;
            if (owner.HasExited)
            {
                return PowerScriptsProtocol.ExitCode.Error;
            }
        }

        using var stopEvent = stopEventHandle is { } handle ? new SessionStopEvent(handle) : null;
        var stopRegistration = stopEvent is null ? null :
            ThreadPool.RegisterWaitForSingleObject(stopEvent, (_, _) => Stop(), null, Timeout.Infinite, executeOnlyOnce: true);
        try
        {
            return RunRequests(invoke);
        }
        finally
        {
            stopRegistration?.Unregister(null);
        }
    }

    private static void Stop()
    {
        using var host = Process.GetCurrentProcess();
        host.Kill(entireProcessTree: true);
    }

    private sealed class SessionStopEvent : WaitHandle
    {
        internal SessionStopEvent(long handle)
        {
            SafeWaitHandle = new SafeWaitHandle(new IntPtr(handle), ownsHandle: true);
            // The private lifetime signal must not flow into interpreter/executor descendants.
            if (!SetHandleInformation(SafeWaitHandle, 1, 0))
            {
                var error = Marshal.GetLastWin32Error();
                Dispose();
                throw new System.ComponentModel.Win32Exception(error);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(SafeWaitHandle handle, uint mask, uint flags);
    }

    private static int RunRequests(Func<string[], int> invoke)
    {
        using var input = System.Console.OpenStandardInput();
        using var output = System.Console.OpenStandardOutput();
        while (PowerScriptsSessionProtocol.ReadAsync(input).GetAwaiter().GetResult() is byte[] request)
        {
            using var document = JsonDocument.Parse(request);
            var root = document.RootElement;
            var arguments = root.GetProperty("arguments").EnumerateArray()
                .Select(value => value.GetString() ?? throw new JsonException("Command arguments must be strings.")).ToArray();
            var payload = root.GetProperty("input");
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            int exitCode;
            using (HostConsole.Capture(payload.ValueKind == JsonValueKind.Null ? string.Empty : payload.GetString()!, stdout, stderr))
            {
                // Execution retains the ordinary trust/consent path. Administrative verbs remain
                // one-shot commands; a session cannot edit preferences or explicitly approve trust.
                if (arguments.Length > 0 &&
                    (arguments[0] is "list" or "run" or "transform" ||
                        (arguments.Length == 2 && arguments[0] == "consumers" && arguments[1] == "list")))
                {
                    exitCode = invoke(arguments);
                }
                else
                {
                    stderr.WriteLine("Host sessions accept list, consumers list, run and transform only.");
                    exitCode = PowerScriptsProtocol.ExitCode.Usage;
                }
            }

            PowerScriptsSessionProtocol.WriteAsync(
                output,
                PowerScriptsSessionProtocol.CreateResponse(exitCode, stdout.ToString(), stderr.ToString())).GetAwaiter().GetResult();
        }

        return 0;
    }
}
