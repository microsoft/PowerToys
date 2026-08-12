// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    internal class ModuleInfo
    {
        public string ExecutableName { get; }

        public string? SubDirectory { get; }

        public string WindowName { get; }

        public ModuleInfo(string executableName, string windowName, string? subDirectory = null)
        {
            ExecutableName = executableName;
            WindowName = windowName;
            SubDirectory = subDirectory;
        }

        /// <summary>
        /// Resolves the ABSOLUTE path to this module's executable in the build under test by walking up
        /// from the test assembly's output folder and returning the first ancestor that actually contains
        /// the exe. This adapts to any build-output nesting — the local flattened
        /// <c>&lt;root&gt;\&lt;plat&gt;\&lt;cfg&gt;\tests\&lt;project&gt;\&lt;tfm&gt;\</c> layout AND the deeper
        /// per-project CI artifact layout — instead of hard-coding the walk-up depth. The old fixed
        /// <c>\..\..\..</c> offset resolved to a non-existent path on CI, so every legacy test failed to
        /// launch PowerToys there; this mirrors the .Next harness's resolver. When no ancestor holds the
        /// exe, it returns the historical fixed-depth path (made absolute) so a launch failure still names
        /// a concrete location.
        /// </summary>
        public string GetDevelopmentPath()
        {
            // 1) Walk up and return the first ancestor that DIRECTLY holds the exe. Covers the flattened
            //    <root>\<plat>\<cfg>\tests\<project>\<tfm>\ layout and the CI artifact layout where the
            //    product's output dir is itself an ancestor of the (deeply nested) test assembly.
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var candidate = ComposeUnder(dir.FullName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 2) Per-project NESTED layout: the test builds under <repoRoot>\src\...\<project>\<plat>\<cfg>\
            //    while the product sits at <repoRoot>\<plat>\<cfg>\ — not a direct ancestor. Probe the
            //    conventional <plat>\<cfg> output beneath each ancestor, preferring this run's own.
            foreach (var platCfg in BuildOutputSubdirs())
            {
                for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                {
                    var candidate = ComposeUnder(Path.Combine(dir.FullName, platCfg));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            // 3) Fallback: the historical fixed-depth walk-up, resolved to an absolute path so a launch
            //    failure still names a concrete location instead of a bare relative fragment.
            var prefix = IsRuntimeIdentifierOutputFolder() ? @"..\..\..\.." : @"..\..\..";
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(baseDir, prefix, RelativeExe()));
        }

        // The exe (optionally under its module subdirectory) directly beneath a build-output root.
        private string ComposeUnder(string root) =>
            string.IsNullOrEmpty(SubDirectory)
                ? Path.Combine(root, ExecutableName)
                : Path.Combine(root, SubDirectory, ExecutableName);

        private string RelativeExe() =>
            string.IsNullOrEmpty(SubDirectory) ? ExecutableName : Path.Combine(SubDirectory, ExecutableName);

        // Conventional <plat>\<cfg> build-output subdirs, ordered to prefer this test run's own so a
        // Debug run resolves the Debug product even when a stale Release build is also present.
        private static string[] BuildOutputSubdirs()
        {
            var all = new[] { @"x64\Debug", @"x64\Release", @"ARM64\Debug", @"ARM64\Release" };
            var baseDir = AppContext.BaseDirectory.Replace('/', '\\');
            var current = all.FirstOrDefault(pc => baseDir.Contains("\\" + pc + "\\", StringComparison.OrdinalIgnoreCase));
            return current is null ? all : new[] { current }.Concat(all.Where(pc => pc != current)).ToArray();
        }

        // True when the executing assembly sits in a RID-specific output subfolder (e.g. ...\<tfm>\win-x64),
        // which a project with a RuntimeIdentifier produces. Used to keep GetDevelopmentPath's relative
        // walk-up correct whether or not the RID subfolder is present.
        private static bool IsRuntimeIdentifierOutputFolder()
        {
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var leaf = Path.GetFileName(baseDir);
            return leaf.Equals("win-x64", StringComparison.OrdinalIgnoreCase)
                || leaf.Equals("win-arm64", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the installed path for this module based on the PowerToys install directory
        /// </summary>
        public string GetInstalledPath(string powerToysInstallPath)
        {
            if (string.IsNullOrEmpty(SubDirectory))
            {
                return Path.Combine(powerToysInstallPath, ExecutableName);
            }

            return Path.Combine(powerToysInstallPath, SubDirectory, ExecutableName);
        }
    }
}
