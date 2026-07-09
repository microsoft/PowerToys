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
        /// Gets the relative development path for this module
        /// </summary>
        public string GetDevelopmentPath()
        {
            // The test assembly normally lives in <buildRoot>\tests\<project>\<tfm>\, so the build
            // output root that holds the module exe is three levels above it. When a test project is
            // built with a RuntimeIdentifier (OutputType=Exe for the MTP runner) the output gains an
            // extra RID subfolder (<tfm>\win-x64\ or \win-arm64\), pushing the root one level further
            // up. Detect that case so the relative path stays correct in both layouts.
            string prefix = IsRuntimeIdentifierOutputFolder() ? @"\..\..\..\.." : @"\..\..\..";

            if (string.IsNullOrEmpty(SubDirectory))
            {
                return $@"{prefix}\{ExecutableName}";
            }

            return $@"{prefix}\{SubDirectory}\{ExecutableName}";
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
