// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// DDC/CI native API wrapper — Win32 primitives only. All retry / fallback
    /// orchestration lives in <see cref="DdcCiController"/>.
    /// </summary>
    public static class DdcCiNative
    {
        /// <summary>
        /// One attempt to get the capabilities string from a physical monitor handle.
        /// Returns null on any failure. The orchestrator owns retry + warn-level
        /// logging; per-attempt failures are logged at Debug here so retries do not
        /// spam the log.
        /// </summary>
        public static string? TryGetCapabilitiesString(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                Logger.LogDebug("DDC: TryGetCapabilitiesString called with IntPtr.Zero");
                return null;
            }

            try
            {
                if (!GetCapabilitiesStringLength(hPhysicalMonitor, out uint length) || length == 0)
                {
                    Logger.LogDebug($"DDC: GetCapabilitiesStringLength failed (handle=0x{hPhysicalMonitor:X}, length={length})");
                    return null;
                }

                var buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (!CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, buffer, length))
                    {
                        Logger.LogDebug($"DDC: CapabilitiesRequestAndCapabilitiesReply failed (handle=0x{hPhysicalMonitor:X})");
                        return null;
                    }

                    return Marshal.PtrToStringAnsi(buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError($"DDC: TryGetCapabilitiesString exception (handle=0x{hPhysicalMonitor:X}): {ex.Message}");
                return null;
            }
        }

        internal static VcpReadAttempt ReadVcpFeature(IntPtr handle, byte code)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, code, IntPtr.Zero, out uint current, out uint maximum))
            {
                return VcpReadAttempt.Success(current, maximum);
            }

            return VcpReadAttempt.Failure(Marshal.GetLastWin32Error());
        }
    }
}
