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

            var handle = this.GetWindowHandle();
            ManagedCommon.WindowHelpers.BringToForeground(handle);

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
    }
}
