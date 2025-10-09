// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace TopToolbar.Services.Workspaces
{
    /// <summary>
    /// Provides shared workspace storage locations used by TopToolbar modules.
    /// </summary>
    internal static class WorkspaceStoragePaths
    {
        private const string CompanyFolder = "Microsoft";
        private const string ModuleFolder = "TopToolbar";
        private const string WorkspacesFolder = "Workspaces";
        private const string WorkspaceFileName = "workspaces.json";

        internal static string GetDefaultWorkspacesPath()
        {
            var baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                CompanyFolder,
                ModuleFolder,
                WorkspacesFolder);

            return Path.Combine(baseDirectory, WorkspaceFileName);
        }

        internal static string GetLegacyPowerToysPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                CompanyFolder,
                "PowerToys",
                WorkspacesFolder,
                WorkspaceFileName);
        }
    }
}
