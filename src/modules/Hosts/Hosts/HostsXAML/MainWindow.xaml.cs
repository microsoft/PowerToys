// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Hosts.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace Hosts
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.SetIcon("Assets/Hosts/Hosts.ico");
            Title = ResourceLoaderInstance.ResourceLoader.GetString("WindowTitle");

            BringToForeground();

            Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitleTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                AppTitleTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
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
