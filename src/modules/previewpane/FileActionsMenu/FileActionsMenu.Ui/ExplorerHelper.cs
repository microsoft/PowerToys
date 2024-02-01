// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileActionsMenu.Ui
{
    public sealed partial class ExplorerHelper
    {
        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // Source: https://stackoverflow.com/questions/14193388/how-to-get-windows-explorers-selected-files-from-within-c
        public static string[] GetSelectedItems()
        {
            string filename;
            List<string> selected = [];
            foreach (SHDocVw.InternetExplorer window in new SHDocVw.ShellWindows())
            {
                filename = Path.GetFileNameWithoutExtension(window.FullName).ToLower(CultureInfo.InvariantCulture);
                if (filename.Equals("explorer", StringComparison.OrdinalIgnoreCase) && window.HWND == GetForegroundWindow())
                {
                    Shell32.IShellFolderViewDual2? folderView = window.Document as Shell32.IShellFolderViewDual2;

                    if (folderView is null)
                    {
                        continue;
                    }

                    var sb = new StringBuilder(260);
                    string windowText;
                    checked
                    {
                        if (GetWindowTextW((IntPtr)window.HWND, sb, sb.Capacity) == 0)
                        {
                            continue;
                        }

                        windowText = sb.ToString();
                    }

                    Shell32.FolderItems items = ((Shell32.IShellFolderViewDual2)window.Document).SelectedItems();

                    // Workaround for selection in multiple tabs
                    if (Path.GetDirectoryName(items.Item(0).Path) != windowText)
                    {
                        continue;
                    }

                    foreach (Shell32.FolderItem item in items)
                    {
                        selected.Add(item.Path);
                    }

                    break;
                }
            }

            return [.. selected];
        }
    }
}
