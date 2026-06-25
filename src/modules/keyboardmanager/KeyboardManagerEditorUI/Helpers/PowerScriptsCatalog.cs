// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Bridges the Keyboard Manager editor to the PowerScripts module.
    ///
    /// PowerScripts are surfaced through the shared executor <c>PowerScripts.Host.exe</c>. To keep the
    /// editor decoupled from the PowerScripts assemblies, we shell out to <c>Host.exe list --json</c>
    /// and parse the result. Selecting a "system" PowerScript in the editor then saves an ordinary
    /// Keyboard Manager "Run Program" mapping whose target is <c>Host.exe run &lt;id&gt;</c>.
    /// </summary>
    public static class PowerScriptsCatalog
    {
        private const string HostExeName = "PowerScripts.Host.exe";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Resolves the full path to <c>PowerScripts.Host.exe</c>, or null if it can't be found.
        /// Search order: explicit override env var, next to the editor, then the default install root.
        /// </summary>
        public static string? ResolveHostPath()
        {
            var overridePath = Environment.GetEnvironmentVariable("POWERSCRIPTS_HOST");
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, HostExeName),
                Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    "PowerScripts",
                    HostExeName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the list of system PowerScripts available for hotkey assignment, or an empty list
        /// when PowerScripts isn't installed or no system scripts exist.
        /// </summary>
        public static IReadOnlyList<PowerScriptInfo> GetSystemScripts()
        {
            var hostPath = ResolveHostPath();
            if (hostPath is null)
            {
                return Array.Empty<PowerScriptInfo>();
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = "list --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return Array.Empty<PowerScriptInfo>();
                }

                string json = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                var all = JsonSerializer.Deserialize<List<PowerScriptInfo>>(json, JsonOptions) ?? new List<PowerScriptInfo>();

                var systemScripts = new List<PowerScriptInfo>();
                foreach (var script in all)
                {
                    if (string.Equals(script.Kind, "system", StringComparison.OrdinalIgnoreCase))
                    {
                        systemScripts.Add(script);
                    }
                }

                return systemScripts;
            }
            catch (Exception)
            {
                // Prototype: a missing/failed PowerScripts host simply yields no scripts to pick.
                return Array.Empty<PowerScriptInfo>();
            }
        }
    }
}
