// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Odotocodot.OneNote.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    internal static class OneNoteItemExtensions
    {
        internal static bool OpenItemInOneNote(this IOneNoteItem item)
        {
            try
            {
                item.OpenInOneNote();
                ShowOneNote();
                return true;
            }
            catch (COMException)
            {
                // The page, section or even notebook may no longer exist, ignore and do nothing.
                return false;
            }
        }

        /// <summary>
        /// Brings OneNote to the foreground and restores it if minimized.
        /// </summary>
        internal static void ShowOneNote()
        {
            using var process = Process.GetProcessesByName("onenote").FirstOrDefault();
            if (process?.MainWindowHandle != null)
            {
                HWND handle = (HWND)process.MainWindowHandle;
                if (PInvoke.IsIconic(handle))
                {
                    PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_RESTORE);
                }

                PInvoke.SetForegroundWindow(handle);
            }
        }
    }
}
