// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Folder.Sources
{
    public class ShellAction : IShellAction
    {
        public bool Execute(string sanitizedPath, IPublicAPI contextApi)
        {
            if (contextApi == null)
            {
                throw new ArgumentNullException(nameof(contextApi));
            }

            return OpenFileOrFolder(sanitizedPath, contextApi);
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

        private static bool OpenFileOrFolder(string path, IPublicAPI contextApi)
        {
            if (!Helper.OpenInShell(path))
            {
                var message = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Properties.Resources.wox_plugin_folder_select_folder_OpenFileOrFolder_error_message, path);
                contextApi.ShowMsg(message);
            }

            return true;
        }
    }
}
