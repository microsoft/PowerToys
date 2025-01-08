﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ManagedCommon
{
    public class WindowHelpers
    {
        public static void BringToForeground(IntPtr handle)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT { type = NativeMethods.INPUTTYPE.INPUT_MOUSE, data = { } };
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);

            NativeMethods.SetForegroundWindow(handle);

            var fgHandle = NativeMethods.GetForegroundWindow();

            if (fgHandle != handle)
            {
                var threadId1 = NativeMethods.GetWindowThreadProcessId(handle, System.IntPtr.Zero);
                var threadId2 = NativeMethods.GetWindowThreadProcessId(fgHandle, System.IntPtr.Zero);

                if (threadId1 != threadId2)
                {
                    NativeMethods.AttachThreadInput(threadId1, threadId2, true);
                    NativeMethods.SetForegroundWindow(handle);
                    NativeMethods.AttachThreadInput(threadId1, threadId2, false);
                }
                else
                {
                    NativeMethods.SetForegroundWindow(handle);
                }
            }
        }

        /// <summary>
        /// Workaround for a WinUI bug on Windows 10 in which a window's top border is always
        /// black. Calls <c>DwmExtendFrameIntoClientArea()</c> with a <c>cyTopHeight</c> of 2 to force
        /// the window's top border to be visible.<br/><br/>
        /// Is a no-op on versions other than Windows 10.
        /// </summary>
        public static void ForceTopBorder1PixelInsetOnWindows10(IntPtr handle)
        {
            if (OSVersionHelper.IsWindows10())
            {
                var margins = new NativeMethods.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyBottomHeight = 0, cyTopHeight = 2 };
                NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
            }
        }
    }
}
