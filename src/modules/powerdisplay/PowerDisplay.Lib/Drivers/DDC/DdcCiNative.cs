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
    /// DDC/CI native API wrapper
    /// </summary>
    public static class DdcCiNative
    {
        /// <summary>
        /// Fetches VCP capabilities string from a monitor and returns a validation result.
        /// This is the slow I2C operation (~4 seconds per monitor) that should only be done once.
        /// The result is cached regardless of success or failure.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>Validation result with capabilities data (or failure status)</returns>
        public static DdcCiValidationResult FetchCapabilities(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                Logger.LogWarning("DDC: Monitor ignored - null physical monitor handle");
                return DdcCiValidationResult.Invalid;
            }

            var handleHex = $"0x{hPhysicalMonitor:X}";

            try
            {
                // Try to get capabilities string (slow I2C operation)
                var capsString = TryGetCapabilitiesString(hPhysicalMonitor);
                if (string.IsNullOrEmpty(capsString))
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - empty capabilities string from DDC/CI");
                    return DdcCiValidationResult.Invalid;
                }

                Logger.LogInfo($"DDC: Capabilities raw (handle={handleHex}, length={capsString.Length}): {capsString}");

                // Parse the capabilities string
                var parseResult = Utils.MccsCapabilitiesParser.Parse(capsString);
                var capabilities = parseResult.Capabilities;

                if (capabilities == null || capabilities.SupportedVcpCodes.Count == 0)
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - parsed capabilities have no VCP codes (parseErrors={parseResult.Errors.Count})");
                    return DdcCiValidationResult.Invalid;
                }

                // Check if brightness (VCP 0x10) is supported - determines DDC/CI validity
                bool supportsBrightness = capabilities.SupportsVcpCode(NativeConstants.VcpCodeBrightness);
                bool supportsContrast = capabilities.SupportsVcpCode(NativeConstants.VcpCodeContrast);
                bool supportsColorTemperature = capabilities.SupportsVcpCode(NativeConstants.VcpCodeSelectColorPreset);
                bool supportsVolume = capabilities.SupportsVcpCode(NativeConstants.VcpCodeVolume);

                Logger.LogInfo(
                    $"DDC: Capabilities parsed (handle={handleHex}) - " +
                    $"Brightness={supportsBrightness} Contrast={supportsContrast} " +
                    $"ColorTemperature={supportsColorTemperature} Volume={supportsVolume}");

                if (!supportsBrightness)
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - brightness (VCP 0x10) not advertised in capabilities");
                }

                return new DdcCiValidationResult(supportsBrightness, capsString, capabilities);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError($"DDC: Monitor ignored (handle={handleHex}) - exception during FetchCapabilities: {ex.Message}");
                return DdcCiValidationResult.Invalid;
            }
        }

        /// <summary>
        /// Try to get capabilities string from a physical monitor handle.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>Capabilities string, or null if failed</returns>
        private static string? TryGetCapabilitiesString(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // Get capabilities string length
                if (!GetCapabilitiesStringLength(hPhysicalMonitor, out uint length) || length == 0)
                {
                    Logger.LogWarning($"DDC: GetCapabilitiesStringLength failed (handle=0x{hPhysicalMonitor:X}, length={length})");
                    return null;
                }

                // Allocate buffer and get capabilities string
                var buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (!CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, buffer, length))
                    {
                        Logger.LogWarning($"DDC: CapabilitiesRequestAndCapabilitiesReply failed (handle=0x{hPhysicalMonitor:X})");
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
    }
}
