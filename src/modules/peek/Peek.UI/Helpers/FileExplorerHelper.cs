// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Peek.Common.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using IServiceProvider = Peek.Common.Models.IServiceProvider;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static Shell32.IShellFolderViewDual2? GetCurrentFolderView()
        {
            var foregroundWindowHandle = PInvoke.GetForegroundWindow();
            var activeTab = GetActiveTabWindowHandle(foregroundWindowHandle);

            var shell = new Shell32.Shell();
            SHDocVw.ShellWindows shellWindows = shell.Windows();
            foreach (SHDocVw.IWebBrowserApp webBrowserApp in shell.Windows())
            {
                var shellFolderView = (Shell32.IShellFolderViewDual2)webBrowserApp.Document;
                var folderTitle = shellFolderView.Folder.Title;

                if (webBrowserApp.HWND == (int)foregroundWindowHandle)
                {
                    var serviceProvider = (IServiceProvider)webBrowserApp;
                    var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);
                    shellBrowser.GetWindow(out IntPtr shellBrowserHandle);

                    if (activeTab == shellBrowserHandle)
                    {
                        return shellFolderView;
                    }
                }
            }

            return null;
        }

        private static HWND GetActiveTabWindowHandle(HWND windowHandle)
        {
            var activeTab = PInvoke.FindWindowEx(new HWND(windowHandle), HWND.Null, "ShellTabWindowClass", null);
            if (activeTab == HWND.Null)
            {
                activeTab = PInvoke.FindWindowEx(new HWND(windowHandle), HWND.Null, "TabWindowClass", null);
            }

            return activeTab;
        }
    }
}
