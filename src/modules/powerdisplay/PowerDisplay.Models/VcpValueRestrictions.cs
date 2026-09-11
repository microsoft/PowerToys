// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Combines per-monitor user restrictions with the built-in hardware rules.
    /// These rules apply to writes only; they do not change VCP reads or discovery.
    /// </summary>
    public static class VcpValueRestrictions
    {
        private const string ResourceName = "PowerDisplay.Models.BuiltInVcpValueBlacklist.json";

        private static readonly Lazy<IReadOnlyList<VcpValueBlacklistEntry>> _hardwareBlocks = new(LoadHardwareBlocks);

        public static bool IsBlocked(string monitorId, byte vcpCode, int value, IEnumerable<VcpValueBlock>? userBlocks)
        {
            return IsBlockedByHardware(monitorId, vcpCode, value)
                || (userBlocks?.Any(block => block != null && block.VcpCode == vcpCode && block.Values.Contains(value)) ?? false);
        }

        public static bool IsBlockedByHardware(string monitorId, byte vcpCode, int value)
        {
            return FindHardwareBlock(monitorId, vcpCode, value) != null;
        }

        /// <summary>
        /// Returns the built-in explanation for a matching restriction, or null when allowed.
        /// Comments include the source issue and are displayed as shipped, like monitor blacklist comments.
        /// </summary>
        public static string? GetHardwareBlockReason(string monitorId, byte vcpCode, int value)
        {
            return FindHardwareBlock(monitorId, vcpCode, value)?.Comments;
        }

        private static VcpValueBlacklistEntry? FindHardwareBlock(string monitorId, byte vcpCode, int value)
        {
            var edidId = MonitorHardwareId.EdidIdFromMonitorId(monitorId);
            if (string.IsNullOrEmpty(edidId))
            {
                return null;
            }

            return _hardwareBlocks.Value.FirstOrDefault(block =>
                string.Equals(block.EdidId, edidId, StringComparison.OrdinalIgnoreCase)
                && block.VcpCode == vcpCode
                && block.Values.Contains(value));
        }

        private static IReadOnlyList<VcpValueBlacklistEntry> LoadHardwareBlocks()
        {
            // Follow the existing embedded monitor blacklist loader: load once, without
            // introducing a logging or reflection-based serialization dependency in Models.
            try
            {
                using var stream = typeof(VcpValueRestrictions).Assembly.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    return Array.Empty<VcpValueBlacklistEntry>();
                }

                var file = JsonSerializer.Deserialize(stream, VcpValueSerializationContext.Default.BuiltInVcpValueBlacklistFile);
                if (file?.Version != 1 || file.Entries == null)
                {
                    return Array.Empty<VcpValueBlacklistEntry>();
                }

                return file.Entries
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.EdidId))
                    .Select(entry => new VcpValueBlacklistEntry
                    {
                        EdidId = entry.EdidId.Trim().ToUpperInvariant(),
                        VcpCode = entry.VcpCode,
                        Values = entry.Values,
                        Comments = entry.Comments ?? string.Empty,
                    })
                    .ToList();
            }
            catch
            {
                return Array.Empty<VcpValueBlacklistEntry>();
            }
        }
    }
}
