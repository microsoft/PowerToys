// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using EnvironmentVariables.Helpers;
using EnvironmentVariables.Helpers.Win32;
using EnvironmentVariables.ViewModels;
using Microsoft.UI.Dispatching;
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

        private static readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private static NativeMethods.WinProc newWndProc;
        private static IntPtr oldWndProc = IntPtr.Zero;

        private void RegisterWindow()
        {
            newWndProc = new NativeMethods.WinProc(WndProc);

            var handle = this.GetWindowHandle();

            oldWndProc = NativeMethods.SetWindowLongPtr(handle, NativeMethods.WindowLongIndexFlags.GWL_WNDPROC, newWndProc);
        }

        private static IntPtr WndProc(IntPtr hWnd, NativeMethods.WindowMessage msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case NativeMethods.WindowMessage.WM_SETTINGSCHANGED:
                    {
                        var lParamStr = Marshal.PtrToStringUTF8(lParam);
                        if (lParamStr == "Environment")
                        {
                            // Do not react on self - not nice, re-check this
                            if (wParam != (IntPtr)0x12345)
                            {
                                var viewModel = App.GetService<MainViewModel>();
                                viewModel.EnvironmentState = Models.EnvironmentState.EnvironmentMessageReceived;
                            }
                        }

                        break;
                    }

                default:
                    break;
            }

            return NativeMethods.CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }
    }
}
