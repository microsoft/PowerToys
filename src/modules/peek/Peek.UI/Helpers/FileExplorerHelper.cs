// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Peek.Common.Models;
using Peek.UI.Native;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static Shell32.IShellFolderViewDual2? GetCurrentFolderView()
        {
            var foregroundWindowHandle = NativeMethods.GetForegroundWindow();
            var lastActiveHandle = NativeMethods.GetTopWindow(foregroundWindowHandle);

            Debug.WriteLine("!~ foregroundwindowhandle: " + foregroundWindowHandle);
            Debug.WriteLine("!~ lastActiveHandle: " + lastActiveHandle);

            var shell = new Shell32.Shell();
            foreach (SHDocVw.InternetExplorer window in shell.Windows())
            {
                // TODO: use a safer casting method
                Debug.WriteLine("!~ menubar" + window.MenuBar);
                Debug.WriteLine("!~ readystate" + window.ReadyState);
                Debug.WriteLine("!~ tlbrowser" + window.TopLevelContainer);
                Debug.WriteLine("!~ visible" + window.Visible);
                var doc = (Shell32.IShellFolderViewDual2)window.Document;
                var focusedItem = doc.FocusedItem;
                Debug.WriteLine("!~ ", focusedItem.Name);
                if (window.HWND == (int)lastActiveHandle)
                {
                    Debug.WriteLine("!~ match");
                }
            }

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
