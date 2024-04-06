// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Shell32;

namespace FileActionsMenu.Ui.Helpers
{
    public sealed class ShortcutHelper
    {
        /// <summary>
        /// Gets the full path from a shortcut path.
        /// </summary>
        /// <param name="shortcutPath">The path of the .lnk file.</param>
        /// <returns>The full path where the shortcut points to.</returns>
        public static string GetFullPathFromShortcut(string shortcutPath)
        {
            if (!shortcutPath.EndsWith(".lnk", System.StringComparison.InvariantCulture))
            {
                return shortcutPath;
            }

            Shell shell = new();
            Folder folder = shell.NameSpace(System.IO.Path.GetDirectoryName(shortcutPath));
            FolderItem folderItem = folder.ParseName(System.IO.Path.GetFileName(shortcutPath));
            if (folderItem != null)
            {
                ShellLinkObject link = (ShellLinkObject)folderItem.GetLink;
                return link.Path;
            }

            return shortcutPath;
        }
    }
}
