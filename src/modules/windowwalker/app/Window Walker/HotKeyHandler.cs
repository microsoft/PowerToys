// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace WindowWalker
{
    /// <summary>
    /// This class handles all hotkey related activities
    /// </summary>
    /// <remarks>Large pieces of this class were retrived from
    /// http://www.dreamincode.net/forums/topic/323708-global-hotkeys-for-wpf-applications-c%23/
    /// </remarks>
    internal class HotKeyHandler
    {
        private readonly IntPtr hwnd;

        /// <summary>
        /// Delegate handler for Hotkey being called
        /// </summary>
        public delegate void HotKeyPressedHandler(object sender, EventArgs e);

        /// <summary>
        /// Event raised when there is an update to the list of open windows
        /// </summary>
        public event HotKeyPressedHandler OnHotKeyPressed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HotKeyHandler"/> class.
        /// Constructor for the class
        /// </summary>
        /// <param name="hwnd">The handle to the window we are registering the key for</param>
        public HotKeyHandler(Visual window)
        {
            hwnd = new WindowInteropHelper((Window)window).Handle;

            if (!(PresentationSource.FromVisual(window) is HwndSource source))
            {
                throw new Exception("Could not create hWnd source from window.");
            }

            source.AddHook(WndProc);

            bool result = Components.InteropAndHelpers.RegisterHotKey(
                hwnd,
                1,
                (int)Components.InteropAndHelpers.Modifiers.Ctrl | (int)Components.InteropAndHelpers.Modifiers.Win,
                (int)Keys.None);
        }

        /// <summary>
        /// Call back function to detect when the hot key has been called
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled">if a key was called</param>
        /// <returns>if a key was called</returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && OnHotKeyPressed != null)
            {
                OnHotKeyPressed(this, new EventArgs());
            }

            return IntPtr.Zero;
        }
    }
}
