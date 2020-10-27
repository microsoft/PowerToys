// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ManagedCommon
{
    public static class RunnerHelper
    {
        public static void WaitForPowerToysRunner(int powerToysPID, Action act)
        {
            var stackTrace = new StackTrace();
            var assembly = Assembly.GetCallingAssembly().GetName();
            var callingMethod = stackTrace.GetFrame(1).GetMethod().Name;
            PowerToysTelemetry.Log.WriteEvent(new DebugEvent() { Message = $"[{assembly}][{callingMethod}]WaitForPowerToysRunner waiting for Event powerToysPID={powerToysPID}" });
            Task.Run(() =>
            {
                const uint INFINITE = 0xFFFFFFFF;
                const uint WAIT_OBJECT_0 = 0x00000000;
                const uint SYNCHRONIZE = 0x00100000;

                IntPtr powerToysProcHandle = NativeMethods.OpenProcess(SYNCHRONIZE, false, powerToysPID);
                if (NativeMethods.WaitForSingleObject(powerToysProcHandle, INFINITE) == WAIT_OBJECT_0)
                {
                    PowerToysTelemetry.Log.WriteEvent(new DebugEvent() { Message = $"[{assembly}][{callingMethod}]WaitForPowerToysRunner Event Notified powerToysPID={powerToysPID}" });
                    act.Invoke();
                }
            });
        }
    }
}
