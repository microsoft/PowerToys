// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using EnvironmentVariables.Helpers;
using WinUIEx;

namespace EnvironmentVariables
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);

            AppWindow.SetIcon("Assets/EnvironmentVariables/EnvironmentVariables.ico");
            var loader = ResourceLoaderInstance.ResourceLoader;
            var title = App.GetService<IElevationHelper>().IsElevated ? loader.GetString("WindowAdminTitle") : loader.GetString("WindowTitle");
            Title = title;
            AppTitleTextBlock.Text = title;

            RegisterWindow();
        }

        private static WinProc newWndProc;
        private static IntPtr oldWndProc = IntPtr.Zero;

        private delegate IntPtr WinProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        [DllImport("User32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        private void RegisterWindow()
        {
            newWndProc = new WinProc(WndProc);

            var handle = this.GetWindowHandle();

            oldWndProc = SetWindowLongPtr(handle, WindowLongIndexFlags.GWL_WNDPROC, newWndProc);
        }

        private static IntPtr WndProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WindowMessage.WM_SETTINGSCHANGED:
                    {
                        var asd = Marshal.PtrToStringUTF8(lParam);
                        _ = asd.Substring(0, asd.Length - 1);
                        break;
                    }

                default:
                    break;
            }

            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        [Flags]
        private enum WindowLongIndexFlags : int
        {
            GWL_WNDPROC = -4,
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, newProc);
            }
            else
            {
                return new IntPtr(SetWindowLong32(hWnd, nIndex, newProc));
            }
        }

        private enum WindowMessage : int
        {
            WM_SETTINGSCHANGED = 0x001A,
        }
    }
}
