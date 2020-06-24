using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCommon
{
    public static class RunnerHelper
    {
        public static void WaitForPowerToysRunner(int powerToysPID)
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
                    Environment.Exit(0);
                }
            });
        }
    }
}
