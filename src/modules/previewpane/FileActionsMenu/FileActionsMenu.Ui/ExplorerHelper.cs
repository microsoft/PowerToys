// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FileActionsMenu.Ui
{
    public sealed class ExplorerHelper
    {
        // Source: https://stackoverflow.com/questions/14193388/how-to-get-windows-explorers-selected-files-from-within-c
        public static string[] GetSelectedItems()
        {
            string filename;
            List<string> selected = [];
            foreach (SHDocVw.InternetExplorer window in new SHDocVw.ShellWindows())
            {
                filename = Path.GetFileNameWithoutExtension(window.FullName).ToLower(CultureInfo.InvariantCulture);
                if (filename.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    Shell32.FolderItems items = ((Shell32.IShellFolderViewDual2)window.Document).SelectedItems();
                    foreach (Shell32.FolderItem item in items)
                    {
                        selected.Add(item.Path);
                    }
                }
            }

            return selected.ToArray();
        }
    }
}
