// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Peek.Common.Models;
using Peek.UI.Native;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static Shell32.IShellFolderViewDual2? GetCurrentFolderView()
        {
            var foregroundWindowHandle = NativeMethods.GetForegroundWindow();

            var selectedItems = new List<File>();
            var shell = new Shell32.Shell();
            foreach (SHDocVw.InternetExplorer window in shell.Windows())
            {
                if (window.HWND == (int)foregroundWindowHandle)
                {
                    return (Shell32.IShellFolderViewDual2)window.Document;
                }
            }

            return null;
        }
    }
}
