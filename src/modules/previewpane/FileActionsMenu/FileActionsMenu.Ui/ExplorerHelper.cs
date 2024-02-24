// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Peek.Common.Models;
using Peek.Helpers;

namespace FileActionsMenu.Ui.Helpers
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
                    IShellItemArray? itemArray;
                    checked
                    {
                        itemArray = FileExplorerHelper.GetSelectedItems(new Windows.Win32.Foundation.HWND((IntPtr)window.HWND));
                    }

                    if (itemArray is null || itemArray.GetCount() == 0)
                    {
                        break;
                    }

                    for (int i = 0; i < itemArray.GetCount(); i++)
                    {
                        IShellItem item = itemArray.GetItemAt(i);
                        selected.Add(item.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_FILESYSPATH));
                    }

                    break;
                }
            }

            return [.. selected];
        }
    }
}
