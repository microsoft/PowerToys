// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources
{
    public class ExplorerAction : IExplorerAction
    {
        private const string FileExplorerProgramName = "explorer";

        public bool Execute(string path, IPublicAPI contextApi)
        {
            if (contextApi == null)
            {
                throw new ArgumentNullException(nameof(contextApi));
            }

            return OpenFileOrFolder(FileExplorerProgramName, path, contextApi);
        }

        public bool ExecuteSanitized(string search, IPublicAPI contextApi)
        {
            if (contextApi == null)
            {
                throw new ArgumentNullException(nameof(contextApi));
            }

            return Execute(SanitizedPath(search), contextApi);
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive and instead inform the user of the error")]
        private static bool OpenFileOrFolder(string program, string path, IPublicAPI contextApi)
        {
            try
            {
                Process.Start(program, path);
            }
            catch (Exception e)
            {
                string messageBoxTitle = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.wox_plugin_folder_select_folder_OpenFileOrFolder_error_message, path);
                Log.Exception($"|Microsoft.Plugin.Folder.Main.OpenFileOrFolder| Failed to open {path} in explorer, {e.Message}", e);
                contextApi.ShowMsg(messageBoxTitle, e.Message);
            }

            return true;
        }
    }
}
