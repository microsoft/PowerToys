// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            // Prototype dev fallback: in an in-repo build the Host isn't copied next to the editor,
            // so walk up from the base directory and probe the Host project's bin output. This keeps
            // the PowerScript action usable for end-to-end testing from a Debug build.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                foreach (var config in new[] { "Debug", "Release" })
                {
                    var hostBin = Path.Combine(
                        dir.FullName,
                        "src",
                        "modules",
                        "PowerScripts",
                        "PowerScripts.Host",
                        "bin",
                        config);

                    if (Directory.Exists(hostBin))
                    {
                        var found = Directory
                            .EnumerateFiles(hostBin, HostExeName, SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(found))
                        {
                            candidates.Add(found);
                        }
                    }
                }

                dir = dir.Parent;
            }

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

        /// <summary>
        /// Records the user's trust for a script's current content, so the engine can run it silently
        /// via <c>Host.exe run &lt;id&gt; --no-consent</c>. Assigning a script to a hotkey is itself an
        /// explicit consent, so we approve it here rather than popping a dialog from the (hidden)
        /// engine-launched process. If the script's contents later change, the non-interactive run
        /// simply no-ops until the user re-assigns it.
        /// </summary>
        public static void ApproveTrust(string id)
        {
            var hostPath = ResolveHostPath();
            if (hostPath is null || string.IsNullOrEmpty(id))
            {
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = $"trust approve {id}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Prototype: if trust can't be recorded the hotkey just won't run; not fatal.
            }
        }

        /// <summary>
        /// True when a Keyboard Manager "Run Program" mapping actually launches a PowerScript, i.e. its
        /// program path is <c>PowerScripts.Host.exe</c>. Used to present these mappings as first-class
        /// PowerScript actions instead of raw run-program cards.
        /// </summary>
        public static bool IsPowerScriptProgramPath(string? programPath) =>
            !string.IsNullOrEmpty(programPath) &&
            string.Equals(Path.GetFileName(programPath), HostExeName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Extracts the script id from a PowerScript mapping's arguments (<c>run &lt;id&gt; ...</c>),
        /// or null if the arguments aren't a recognizable PowerScript run command.
        /// </summary>
        public static string? ParseScriptId(string? programArgs)
        {
            if (string.IsNullOrWhiteSpace(programArgs))
            {
                return null;
            }

            var tokens = programArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2 && string.Equals(tokens[0], "run", StringComparison.OrdinalIgnoreCase))
            {
                return tokens[1];
            }

            return null;
        }

        /// <summary>Resolves a script's display name from its id, falling back to the id itself.</summary>
        public static string GetScriptName(string id)
        {
            foreach (var script in GetSystemScripts())
            {
                if (string.Equals(script.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return script.Name;
                }
            }

            return id;
        }
    }
}
