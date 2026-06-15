// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace KeyboardManagerEditorUI.Templates
{
    /// <summary>
    /// Resolves the on-disk location of a PowerToys executable referenced by a command template.
    /// Templates ship a per-user (<c>%LOCALAPPDATA%</c>) path by default; this retargets to a
    /// machine-wide (<c>%ProgramFiles%</c>) install when the per-user path is absent so the saved
    /// mapping works regardless of install scope.
    /// </summary>
    internal static class PowerToysInstallResolver
    {
        // Candidate install locations, in preference order. Env-var form is preserved in the saved
        // value so the engine expands it at trigger time (and it survives a reinstall in place).
        private static readonly string[] Candidates =
        {
            @"%LOCALAPPDATA%\PowerToys\PowerToys.exe",
            @"%ProgramFiles%\PowerToys\PowerToys.exe",
            @"%ProgramW6432%\PowerToys\PowerToys.exe",
        };

        /// <summary>
        /// Returns an executable path that exists on disk. If <paramref name="executable"/> already
        /// resolves to an existing file it is returned unchanged. For a non-existent
        /// <c>PowerToys.exe</c> path, known install locations are probed. If none exist the original
        /// value is returned (the engine will surface a "program not found" error at trigger time).
        /// </summary>
        public static string ResolveExecutable(string executable)
        {
            if (string.IsNullOrEmpty(executable))
            {
                return executable;
            }

            if (File.Exists(Environment.ExpandEnvironmentVariables(executable)))
            {
                return executable;
            }

            // Only retarget the known PowerToys.exe; leave arbitrary executables untouched.
            string fileName = Path.GetFileName(Environment.ExpandEnvironmentVariables(executable));
            if (!string.Equals(fileName, "PowerToys.exe", StringComparison.OrdinalIgnoreCase))
            {
                return executable;
            }

            foreach (var candidate in Candidates)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(candidate)))
                {
                    return candidate;
                }
            }

            return executable;
        }
    }
}
