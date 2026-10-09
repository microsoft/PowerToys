// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using Peek.Common.Models;
using Peek.UI.Extensions;
using SHDocVw;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

using IServiceProvider = Peek.Common.Models.IServiceProvider;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        internal static IShellItemArray? GetSelectedItems(HWND foregroundWindowHandle)
        {
            return GetItemsInternal(foregroundWindowHandle, onlySelectedFiles: true);
        }

        internal static IShellItemArray? GetItems(HWND foregroundWindowHandle)
        {
            return GetItemsInternal(foregroundWindowHandle, onlySelectedFiles: false);
        }

        private static IShellItemArray? GetItemsInternal(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            // If the caret is visible, we assume the user is typing and we don't want to interfere with that
            if (CaretVisible(foregroundWindowHandle))
            {
                return null;
            }
            else if (foregroundWindowHandle.IsDesktopWindow())
            {
                return GetItemsFromDesktop(foregroundWindowHandle, onlySelectedFiles);
            }
            else
            {
                return GetItemsFromFileExplorer(foregroundWindowHandle, onlySelectedFiles);
            }
        }

        private static IShellItemArray? GetItemsFromDesktop(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            const int SWC_DESKTOP = 8;
            const int SWFO_NEEDDISPATCH = 1;

            var shell = new Shell32.Shell();
            ShellWindows shellWindows = shell.Windows();

            object? oNull1 = null;
            object? oNull2 = null;

            var serviceProvider = (IServiceProvider)shellWindows.FindWindowSW(ref oNull1, ref oNull2, SWC_DESKTOP, out int pHWND, SWFO_NEEDDISPATCH);
            var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke_PeekUI.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);

            IShellItemArray? shellItemArray = GetShellItemArray(shellBrowser, onlySelectedFiles);
            return shellItemArray;
        }

        private static IShellItemArray? GetItemsFromFileExplorer(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            IShellItemArray? shellItemArray = null;

            var activeTab = foregroundWindowHandle.GetActiveTab();

            var shell = new Shell32.Shell();
            ShellWindows shellWindows = shell.Windows();
            foreach (IWebBrowserApp webBrowserApp in shellWindows)
            {
                if (webBrowserApp.Document is Shell32.IShellFolderViewDual2 shellFolderView)
                {
                    var folderTitle = shellFolderView.Folder.Title;

                    if (webBrowserApp.HWND == foregroundWindowHandle)
                    {
                        var serviceProvider = (IServiceProvider)webBrowserApp;
                        var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke_PeekUI.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);
                        shellBrowser.GetWindow(out IntPtr shellBrowserHandle);

                        if (activeTab == shellBrowserHandle)
                        {
                            shellItemArray = GetShellItemArray(shellBrowser, onlySelectedFiles);
                            return shellItemArray;
                        }
                    }
                }
            }

            return shellItemArray;
        }

        private static IShellItemArray? GetShellItemArray(IShellBrowser shellBrowser, bool onlySelectedFiles)
        {
            var shellViewObject = shellBrowser.QueryActiveShellView();
            var shellView = shellViewObject as IFolderView;
            if (shellView != null)
            {
                var selectionFlag = onlySelectedFiles ? (uint)_SVGIO.SVGIO_SELECTION : (uint)_SVGIO.SVGIO_ALLVIEW;
                shellView.ItemCount(selectionFlag, out var countItems);
                if (countItems > 0)
                {
                    shellView.Items(selectionFlag, typeof(IShellItemArray).GUID, out var items);
                    return items as IShellItemArray;
                }
            }

            return null;
        }

        /// <summary>
        /// Heuristic to decide whether the user is actively typing so we should suppress Peek activation.
        /// Current logic:
        ///  - If the focused control class name contains "Edit" or "Input" (e.g. Explorer search box or in-place rename), return true.
        ///  - Otherwise fall back to the legacy GUI_CARETBLINKING flag (covers other text contexts where class name differs but caret blinks).
        ///  - If we fail to retrieve GUI thread info, we default to false (do not suppress) to avoid blocking activation due to transient failures.
        /// NOTE: This intentionally no longer walks ancestor chains; any Edit/Input focus inside the same top-level Explorer/Desktop window is treated as typing.
        /// </summary>
        private static unsafe bool CaretVisible(HWND hwnd)
        {
            GUITHREADINFO gi = new() { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            if (!PInvoke_PeekUI.GetGUIThreadInfo(0, ref gi))
            {
                return false; // fail open (allow activation)
            }

            // Quick sanity: restrict to same top-level window (match prior behavior)
            if (gi.hwndActive != hwnd)
            {
                return false;
            }

            HWND focus = gi.hwndFocus;
            if (focus == HWND.Null)
            {
                return false;
            }

            // Get focused window class (96 chars buffer; GetClassNameW bounds writes). Treat any class containing
            // "Edit" or "Input" as a text field (search / titlebar) and suppress Peek.
            Span<char> buf = stackalloc char[96];
            fixed (char* p = buf)
            {
                int len = PInvoke_PeekUI.GetClassName(focus, p, buf.Length);
                if (len > 0)
                {
                    var focusClass = new string(p, 0, len);
                    if (focusClass.Contains("Edit", StringComparison.OrdinalIgnoreCase) || focusClass.Contains("Input", StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // treat any Edit/Input focus as typing.
                    }
                    else
                    {
                        ManagedCommon.Logger.LogDebug($"Peek suppression: focus class{focusClass}");
                    }
                }
            }

            // Fallback: original caret blinking heuristic for other text-entry contexts
            return (gi.flags & GUITHREADINFO_FLAGS.GUI_CARETBLINKING) != 0;
        }
    }
}
