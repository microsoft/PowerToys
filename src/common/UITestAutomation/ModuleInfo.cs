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
            if (string.IsNullOrEmpty(SubDirectory))
            {
                return $@"\..\..\..\{ExecutableName}";
            }

            return $@"\..\..\..\{SubDirectory}\{ExecutableName}";
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
