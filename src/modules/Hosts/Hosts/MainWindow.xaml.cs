// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Hosts.Helpers;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Hosts
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                SetTitleBar();
            }
            else
            {
                titleBar.Visibility = Visibility.Collapsed;

                // Set window icon
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.SetIcon("Assets/Hosts.ico");

                if (ThemeHelpers.GetAppTheme() == AppTheme.Dark)
                {
                    ThemeHelpers.SetImmersiveDarkMode(hWnd, true);
                }
            }

            BringToForeground();
        }

        private void SetTitleBar()
        {
            AppWindow window = this.GetAppWindow();
            window.TitleBar.ExtendsContentIntoTitleBar = true;
            window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            SetTitleBar(titleBar);
        }

        private void BringToForeground()
        {
            var handle = this.GetWindowHandle();
            var fgHandle = NativeMethods.GetForegroundWindow();

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
}
