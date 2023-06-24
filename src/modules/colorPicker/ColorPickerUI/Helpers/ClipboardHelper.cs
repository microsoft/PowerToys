// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using ManagedCommon;
using static ColorPicker.NativeMethods;

namespace ColorPicker.Helpers
{
    public static class ClipboardHelper
    {
        /// <summary>
        /// Defined error code for "clipboard can't open"
        /// </summary>
        private const uint ErrorCodeClipboardCantOpen = 0x800401D0;

        public static void CopyToClipboard(string colorRepresentationToCopy)
        {
            if (!string.IsNullOrEmpty(colorRepresentationToCopy))
            {
                // nasty hack - sometimes clipboard can be in use and it will raise and exception
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(colorRepresentationToCopy);
                        break;
                    }
                    catch (COMException ex)
                    {
                        var hwnd = GetOpenClipboardWindow();
                        var sb = new StringBuilder(501);
                        _ = GetWindowText(hwnd.ToInt32(), sb, 500);
                        var applicationUsingClipboard = sb.ToString();

                        if ((uint)ex.ErrorCode != ErrorCodeClipboardCantOpen)
                        {
                            Logger.LogError("Failed to set text into clipboard", ex);
                        }
                        else
                        {
                            Logger.LogError("Failed to set text into clipboard, application that is locking clipboard - " + applicationUsingClipboard, ex);
                        }
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }
        }
    }
}
