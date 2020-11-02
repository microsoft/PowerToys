// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Folder.Sources
{
    public class ShellAction : IShellAction
    {
        public bool Execute(string path, IPublicAPI contextApi)
        {
            if (contextApi == null)
            {
                throw new ArgumentNullException(nameof(contextApi));
            }

            return OpenFileOrFolder(path, contextApi);
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
            // Using Ordinal since this is internal and used with a symbol
            if (!sanitizedPath.StartsWith("\\", StringComparison.Ordinal))
            {
                return sanitizedPath;
            }

            return sanitizedPath.Insert(0, "\\");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive and instead inform the user of the error")]
        private static bool OpenFileOrFolder(string path, IPublicAPI contextApi)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = path;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                }
            }
            catch (Exception e)
            {
                string messageBoxTitle = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.wox_plugin_folder_select_folder_OpenFileOrFolder_error_message, path);
                Log.Exception($"Failed to open {path}, {e.Message}", e, MethodBase.GetCurrentMethod().DeclaringType);
                contextApi.ShowMsg(messageBoxTitle, e.Message);
            }

            return true;
        }
    }
}
