// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Loads the built-in monitor blacklist shipped with PowerToys.
    /// The data is an embedded JSON resource in this assembly; the file is read once
    /// on first access and cached for the lifetime of the process.
    /// </summary>
    /// <remarks>
    /// Loader failures are non-fatal: on any exception (missing resource, malformed
    /// JSON, etc.) the loader returns an empty list. This keeps PowerDisplay running
    /// even if a malformed release ships, and avoids logging dependencies inside the
    /// AOT-compatible PowerDisplay.Models assembly.
    /// </remarks>
    public static class BuiltInMonitorBlacklist
    {
        private const string ResourceName = "PowerDisplay.Models.BuiltInMonitorBlacklist.json";

        private static readonly Lazy<IReadOnlyList<MonitorBlacklistEntry>> _entries
            = new(LoadFromResource);

        public static IReadOnlyList<MonitorBlacklistEntry> Entries => _entries.Value;

        private static IReadOnlyList<MonitorBlacklistEntry> LoadFromResource()
        {
            try
            {
                var assembly = typeof(BuiltInMonitorBlacklist).Assembly;
                using var stream = assembly.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    return Array.Empty<MonitorBlacklistEntry>();
                }

                var file = JsonSerializer.Deserialize(
                    stream,
                    MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile);

                if (file?.Entries == null)
                {
                    return Array.Empty<MonitorBlacklistEntry>();
                }

                // Only the v1 schema is understood by this build. Future versions
                // ship a refreshed binary that updates this check.
                if (file.Version != 1)
                {
                    return Array.Empty<MonitorBlacklistEntry>();
                }

                return file.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.EdidId))
                    .Select(e => new MonitorBlacklistEntry
                    {
                        EdidId = e.EdidId.Trim().ToUpperInvariant(),
                        Comments = e.Comments ?? string.Empty,
                    })
                    .ToList();
            }
            catch
            {
                return Array.Empty<MonitorBlacklistEntry>();
            }
        }
    }
}
