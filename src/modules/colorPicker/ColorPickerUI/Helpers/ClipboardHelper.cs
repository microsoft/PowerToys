// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using static ColorPicker.NativeMethods;

namespace ColorPicker.Helpers
{
    public static class ClipboardHelper
    {
        private const uint ErrorCodeClipboardCantOpen = 0x800401D0;
        private static readonly List<string> _colorHistory = new List<string>();

        public static void CopyToClipboard(string colorRepresentationToCopy)
        {
            if (!string.IsNullOrEmpty(colorRepresentationToCopy))
            {
                // ذخیره در لیست تاریخچه (بدون نیاز به فایل جداگانه)
                if (!_colorHistory.Contains(colorRepresentationToCopy))
                {
                    _colorHistory.Insert(0, colorRepresentationToCopy);
                    if (_colorHistory.Count > 10) _colorHistory.RemoveAt(10);
                }

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(colorRepresentationToCopy, true);
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

        public static List<string> GetHistory() => _colorHistory.ToList();
    }
}
