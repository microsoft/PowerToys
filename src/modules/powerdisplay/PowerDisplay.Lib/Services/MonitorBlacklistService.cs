// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Decides whether a monitor identified by its <c>Monitor.Id</c> (or raw DevicePath)
    /// should be filtered out of PowerDisplay's discovery. Matches on EdidId only —
    /// model-level granularity, so a single entry covers every physical port and every
    /// machine with the same monitor model.
    /// </summary>
    /// <remarks>
    /// Only the built-in list shipped with PowerToys is consulted; user-customized
    /// blacklists were considered but cut due to UI cost. EdidIds are normalized
    /// (trimmed, upper-cased) on construction; comparisons use
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> as defense in depth.
    /// </remarks>
    public sealed class MonitorBlacklistService
    {
        private readonly HashSet<string> _blockedEdidIds;

        public MonitorBlacklistService()
        {
            _blockedEdidIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in BuiltInMonitorBlacklist.Entries)
            {
                AddNormalized(entry.EdidId);
            }
        }

        /// <summary>
        /// Returns true if <paramref name="monitorId"/> (a <c>Monitor.Id</c> or raw Windows
        /// DevicePath) has an EdidId in the built-in blacklist. Monitors whose EdidId cannot
        /// be extracted (empty path, malformed) are never blocked — we only filter what we
        /// can positively identify.
        /// </summary>
        public bool IsBlocked(string monitorId)
        {
            var edid = MonitorIdentity.EdidIdFromMonitorId(monitorId);
            return !string.IsNullOrEmpty(edid) && _blockedEdidIds.Contains(edid);
        }

        private void AddNormalized(string? edidId)
        {
            var trimmed = edidId?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                _blockedEdidIds.Add(trimmed.ToUpperInvariant());
            }
        }
    }
}
