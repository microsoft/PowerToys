// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;

namespace Microsoft.Plugin.Folder.Sources
{
    public class ExplorerAction : IExplorerAction
    {
        private const string FileExplorerProgramName = "explorer";

        public bool Execute(string path)
        {
            Process.Start(FileExplorerProgramName, path);
            return true;
        }

        public bool ExecuteSanitized(string search)
        {
            return Execute(SanitizedPath(search));
        }

        public bool ExecuteWithCatch(string filePath)
        {
            try
            {
                return Execute(filePath);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Could not start " + filePath);
            }

            return true;
        }

        public bool ExecuteSanitizedWithCatch(string filePath)
        {
            try
            {
                return ExecuteSanitized(filePath);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Could not start " + filePath);
            }

            return true;
        }

        private static string SanitizedPath(string search)
        {
            var sanitizedPath = Regex.Replace(search, @"[\/\\]+", "\\");

            // A network path must start with \\
            if (!sanitizedPath.StartsWith("\\", StringComparison.InvariantCulture))
            {
                return sanitizedPath;
            }

            return sanitizedPath.Insert(0, "\\");
        }
    }
}
