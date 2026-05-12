// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// DDC/CI native API wrapper — Win32 primitives only. All retry / fallback
    /// orchestration lives in <see cref="DdcCiController"/>.
    /// </summary>
    public static class DdcCiNative
    {
        // Continuous-range VCP features probed when running in max-compatibility mode.
        // Discrete-value features (0x14 color preset, 0x60 input source) are excluded
        // — GetVCPFeatureAndVCPFeatureReply returns only current+max, so we cannot
        // synthesize a meaningful supported-value list for them. Friendly names come
        // from VcpNames.GetCodeName to keep a single source of truth.
        private static readonly byte[] ProbeableContinuousVcpCodes =
        {
            VcpCodeBrightness,
            VcpCodeContrast,
            VcpCodeVolume,
            VcpCodePowerMode,
        };

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

        /// <summary>
        /// Sequentially probes each VCP code in <see cref="ProbeableContinuousVcpCodes"/>
        /// via GetVCPFeatureAndVCPFeatureReply. Used as the max-compatibility-mode fallback
        /// when the cap string is empty or unparseable. Returns null if zero probes succeed;
        /// otherwise returns a synthetic <see cref="VcpCapabilities"/> with only the codes
        /// that responded.
        /// </summary>
        /// <remarks>
        /// Sequential, not parallel — physical monitors share an I²C arbitration bus,
        /// concurrent reads cause spurious failures.
        /// </remarks>
        public static VcpCapabilities? ProbeSupportedVcpFeatures(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                Logger.LogDebug("DDC: ProbeSupportedVcpFeatures called with IntPtr.Zero");
                return null;
            }

            var caps = new VcpCapabilities();

            foreach (var code in ProbeableContinuousVcpCodes)
            {
                try
                {
                    if (GetVCPFeatureAndVCPFeatureReply(hPhysicalMonitor, code, IntPtr.Zero, out uint _, out uint _))
                    {
                        caps.SupportedVcpCodes[code] = new VcpCodeInfo(code, VcpNames.GetCodeName(code));
                    }
                    else
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        Logger.LogDebug($"DDC: [max-compat] probe of VCP 0x{code:X2} failed (handle=0x{hPhysicalMonitor:X}, error={lastError})");
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Logger.LogError($"DDC: [max-compat] probe of VCP 0x{code:X2} threw (handle=0x{hPhysicalMonitor:X}): {ex.Message}");
                }
            }

            return caps.SupportedVcpCodes.Count > 0 ? caps : null;
        }
    }
}
