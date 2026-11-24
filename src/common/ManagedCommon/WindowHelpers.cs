// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ManagedCommon
{
    public class WindowHelpers
    {
        // SendInput normally returns in well under a millisecond; this only bounds how long the caller (usually a UI
        // thread) is held up when the input stack stalls.
        private static readonly TimeSpan ForegroundInputTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly ForegroundInputInjector DefaultForegroundInputInjector = new(SendForegroundInput);

        public static void BringToForeground(IntPtr handle)
        {
            BringToForeground(handle, DefaultForegroundInputInjector, ForegroundInputTimeout);
        }

        internal static void BringToForeground(IntPtr handle, ForegroundInputInjector foregroundInputInjector, TimeSpan foregroundInputTimeout)
        {
            // There's no foreground lock to get past if the window is already in the foreground, so don't send input.
            if (handle != IntPtr.Zero && NativeMethods.GetForegroundWindow() == handle)
            {
                return;
            }

            // The input makes this process the one that received the last input event, which lets SetForegroundWindow
            // get past the foreground lock.
            foregroundInputInjector.TrySend(foregroundInputTimeout);

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
        /// WinUI issue: https://github.com/microsoft/microsoft-ui-xaml/issues/6901
        /// </summary>
        public static void ForceTopBorder1PixelInsetOnWindows10(IntPtr handle)
        {
            if (OSVersionHelper.IsWindows10())
            {
                var margins = new NativeMethods.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyBottomHeight = 0, cyTopHeight = 2 };
                NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
            }
        }

        private static void SendForegroundInput()
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT { type = NativeMethods.INPUTTYPE.INPUT_MOUSE, data = { } };
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);
        }
    }
}
